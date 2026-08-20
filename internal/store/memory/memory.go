package memory

import (
	"context"
	"slices"
	"sync"

	"github.com/mortenaho/workflowengine/internal/domain"
	"github.com/mortenaho/workflowengine/internal/store"
)

type Store struct {
	mu        sync.Mutex
	defs      map[string]*domain.Definition
	instances map[string]*domain.ProcessInstance
	tasks     map[string]*domain.Task
}

func New() *Store {
	return &Store{
		defs:      make(map[string]*domain.Definition),
		instances: make(map[string]*domain.ProcessInstance),
		tasks:     make(map[string]*domain.Task),
	}
}

var _ store.Store = (*Store)(nil)

func cloneDef(d *domain.Definition) *domain.Definition {
	if d == nil {
		return nil
	}
	cp := *d
	return &cp
}

func cloneInst(i *domain.ProcessInstance) *domain.ProcessInstance {
	if i == nil {
		return nil
	}
	cp := *i
	cp.Parameters = i.Parameters.Clone()
	return &cp
}

func cloneTask(t *domain.Task) *domain.Task {
	if t == nil {
		return nil
	}
	cp := *t
	if t.CompletedAt != nil {
		ts := *t.CompletedAt
		cp.CompletedAt = &ts
	}
	return &cp
}

func (s *Store) SaveDefinition(_ context.Context, def *domain.Definition) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.defs[def.ID] = cloneDef(def)
	return nil
}

func (s *Store) GetDefinition(_ context.Context, id string) (*domain.Definition, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	d, ok := s.defs[id]
	if !ok {
		return nil, domain.ErrNotFound
	}
	return cloneDef(d), nil
}

func (s *Store) GetDefinitionByKey(_ context.Context, tenantID, key string) (*domain.Definition, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	tenantID = domain.NormalizeTenant(tenantID)
	var found *domain.Definition
	for _, d := range s.defs {
		if domain.NormalizeTenant(d.TenantID) == tenantID && d.Key == key {
			if found == nil || d.CreatedAt.After(found.CreatedAt) {
				found = d
			}
		}
	}
	if found == nil {
		return nil, domain.ErrNotFound
	}
	return cloneDef(found), nil
}

func (s *Store) CreateInstance(_ context.Context, inst *domain.ProcessInstance) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.instances[inst.ID] = cloneInst(inst)
	return nil
}

func (s *Store) GetInstance(_ context.Context, id string) (*domain.ProcessInstance, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	inst, ok := s.instances[id]
	if !ok {
		return nil, domain.ErrNotFound
	}
	return cloneInst(inst), nil
}

func (s *Store) UpdateInstance(_ context.Context, inst *domain.ProcessInstance) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	if _, ok := s.instances[inst.ID]; !ok {
		return domain.ErrNotFound
	}
	s.instances[inst.ID] = cloneInst(inst)
	return nil
}

func (s *Store) ListRootInstances(_ context.Context, tenantID, processKey string) ([]*domain.ProcessInstance, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	tenantID = domain.NormalizeTenant(tenantID)
	out := make([]*domain.ProcessInstance, 0)
	for _, inst := range s.instances {
		if domain.NormalizeTenant(inst.TenantID) != tenantID {
			continue
		}
		if inst.DefinitionKey != processKey {
			continue
		}
		if inst.ParentInstanceID != "" {
			continue
		}
		out = append(out, cloneInst(inst))
	}
	slices.SortFunc(out, func(a, b *domain.ProcessInstance) int {
		return b.CreatedAt.Compare(a.CreatedAt)
	})
	return out, nil
}

func (s *Store) SaveTask(_ context.Context, task *domain.Task) error {
	s.mu.Lock()
	defer s.mu.Unlock()
	s.tasks[task.ID] = cloneTask(task)
	return nil
}

func (s *Store) GetTask(_ context.Context, id string) (*domain.Task, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	t, ok := s.tasks[id]
	if !ok {
		return nil, domain.ErrNotFound
	}
	return cloneTask(t), nil
}

func (s *Store) TransitionTask(_ context.Context, id string, allowed []domain.TaskStatus, apply func(*domain.Task) error) (*domain.Task, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	t, ok := s.tasks[id]
	if !ok {
		return nil, domain.ErrNotFound
	}
	if !slices.Contains(allowed, t.Status) {
		return nil, domain.ErrNotOpen
	}
	cp := cloneTask(t)
	if err := apply(cp); err != nil {
		return nil, err
	}
	s.tasks[id] = cloneTask(cp)
	return cloneTask(cp), nil
}

func (s *Store) ListTasks(_ context.Context, filter domain.TaskFilter) ([]*domain.Task, error) {
	s.mu.Lock()
	defer s.mu.Unlock()
	out := make([]*domain.Task, 0)
	for _, t := range s.tasks {
		if !matchTask(t, filter) {
			continue
		}
		out = append(out, cloneTask(t))
	}
	return out, nil
}

func matchTask(t *domain.Task, f domain.TaskFilter) bool {
	if f.TenantID != "" && domain.NormalizeTenant(t.TenantID) != domain.NormalizeTenant(f.TenantID) {
		return false
	}
	if f.InstanceID != "" && t.InstanceID != f.InstanceID && t.ParentInstanceID != f.InstanceID {
		return false
	}
	if f.Status != "" && t.Status != f.Status {
		return false
	}
	if len(f.Statuses) > 0 && !slices.Contains(f.Statuses, t.Status) {
		return false
	}
	if f.ClaimedBy != "" && t.ClaimedBy != f.ClaimedBy {
		return false
	}
	if f.GroupID != "" {
		return t.AssigneeKind == domain.AssigneeGroup && t.AssigneeID == f.GroupID
	}
	if f.UserID != "" {
		if t.AssigneeKind == domain.AssigneeUser && t.AssigneeID == f.UserID {
			return true
		}
		if t.AssigneeKind == domain.AssigneeGroup && slices.Contains(f.GroupIDs, t.AssigneeID) {
			return true
		}
		return false
	}
	return true
}
