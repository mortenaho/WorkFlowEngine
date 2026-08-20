package engine

import (
	"context"
	"fmt"
	"strings"
	"time"

	"github.com/mortenaho/workflowengine/internal/domain"
	"github.com/mortenaho/workflowengine/internal/identity"
	"github.com/mortenaho/workflowengine/internal/store"
)

type Option func(*Engine)

func WithClock(clock func() time.Time) Option {
	return func(e *Engine) { e.clock = clock }
}

type Engine struct {
	store store.Store
	dir   identity.Directory
	clock func() time.Time
}

func New(s store.Store, d identity.Directory, opts ...Option) *Engine {
	e := &Engine{store: s, dir: d, clock: time.Now}
	for _, opt := range opts {
		opt(e)
	}
	return e
}

func (e *Engine) now() time.Time { return e.clock().UTC() }

func (e *Engine) Register(ctx context.Context, key, name string) (*domain.Definition, error) {
	key = strings.TrimSpace(key)
	if key == "" {
		return nil, fmt.Errorf("%w: process key is required", domain.ErrInvalid)
	}
	if existing, err := e.store.GetDefinitionByKey(ctx, domain.TenantID(ctx), key); err == nil {
		if name != "" && existing.Name != name {
			existing.Name = name
			_ = e.store.SaveDefinition(ctx, existing)
		}
		return existing, nil
	}
	if name == "" {
		name = key
	}
	def := &domain.Definition{
		ID:        domain.NewID(),
		TenantID:  domain.TenantID(ctx),
		Key:       key,
		Name:      name,
		CreatedAt: e.now(),
	}
	if err := e.store.SaveDefinition(ctx, def); err != nil {
		return nil, err
	}
	return def, nil
}

func (e *Engine) GetDefinitionByKey(ctx context.Context, key string) (*domain.Definition, error) {
	return e.store.GetDefinitionByKey(ctx, domain.TenantID(ctx), key)
}

func (e *Engine) Start(ctx context.Context, processKey, initiator string, params domain.Vars) (*domain.StartResult, error) {
	processKey = strings.TrimSpace(processKey)
	initiator = strings.TrimSpace(initiator)
	if processKey == "" {
		return nil, fmt.Errorf("%w: processKey is required", domain.ErrInvalid)
	}
	if initiator == "" {
		return nil, fmt.Errorf("%w: initiator is required", domain.ErrInvalid)
	}
	def, err := e.Register(ctx, processKey, "")
	if err != nil {
		return nil, err
	}
	now := e.now()
	inst := &domain.ProcessInstance{
		ID:            domain.NewID(),
		TenantID:      domain.TenantID(ctx),
		DefinitionID:  def.ID,
		DefinitionKey: def.Key,
		Status:        domain.InstanceRunning,
		Parameters:    params.Clone(),
		StartedBy:     initiator,
		CreatedAt:     now,
		UpdatedAt:     now,
	}
	if err := e.store.CreateInstance(ctx, inst); err != nil {
		return nil, err
	}
	return &domain.StartResult{DefinitionKey: def.Key, InstanceID: inst.ID}, nil
}

func (e *Engine) GetInstance(ctx context.Context, id string) (*domain.ProcessInstance, error) {
	inst, err := e.store.GetInstance(ctx, id)
	if err != nil {
		return nil, err
	}
	if domain.NormalizeTenant(inst.TenantID) != domain.TenantID(ctx) {
		return nil, domain.ErrForbiddenTenant
	}
	return inst, nil
}

func (e *Engine) ListByProcessKey(ctx context.Context, processKey string) (*domain.ProcessList, error) {
	processKey = strings.TrimSpace(processKey)
	if processKey == "" {
		return nil, fmt.Errorf("%w: processKey is required", domain.ErrInvalid)
	}
	roots, err := e.store.ListRootInstances(ctx, domain.TenantID(ctx), processKey)
	if err != nil {
		return nil, err
	}
	list := &domain.ProcessList{
		ProcessKey: processKey,
		Instances:  make([]*domain.ProcessInstanceDetail, 0, len(roots)),
	}
	for _, inst := range roots {
		if domain.NormalizeTenant(inst.TenantID) != domain.TenantID(ctx) {
			continue
		}
		tasks, err := e.store.ListTasks(ctx, domain.TaskFilter{
			InstanceID: inst.ID,
			TenantID:   domain.TenantID(ctx),
		})
		if err != nil {
			return nil, err
		}
		if tasks == nil {
			tasks = []*domain.Task{}
		}
		detail := detailFrom(inst, processKey, tasks)
		list.Instances = append(list.Instances, detail)
	}
	list.Total = len(list.Instances)
	return list, nil
}

func detailFrom(inst *domain.ProcessInstance, processKey string, tasks []*domain.Task) *domain.ProcessInstanceDetail {
	d := &domain.ProcessInstanceDetail{
		InstanceID:    inst.ID,
		ProcessKey:    processKey,
		DefinitionKey: inst.DefinitionKey,
		Initiator:     inst.StartedBy,
		Status:        inst.Status,
		Parameters:    inst.Parameters.Clone(),
		CreatedAt:     inst.CreatedAt,
		UpdatedAt:     inst.UpdatedAt,
		Tasks:         tasks,
		TaskTotal:     len(tasks),
	}
	for _, t := range tasks {
		if t.Status == domain.TaskDone {
			d.TasksCompleted++
		} else {
			d.TasksOpen++
		}
	}
	d.AllTasksCompleted = d.TaskTotal > 0 && d.TasksOpen == 0
	return d
}

func (e *Engine) Refer(ctx context.Context, actor string, in domain.ReferInput) (*domain.ReferResult, error) {
	actor = strings.TrimSpace(actor)
	if actor == "" {
		return nil, fmt.Errorf("%w: actor is required", domain.ErrInvalid)
	}
	defKey := strings.TrimSpace(in.DefinitionKey)
	parentID := strings.TrimSpace(in.ParentInstanceID)
	var parent *domain.ProcessInstance
	if parentID != "" {
		var err error
		parent, err = e.GetInstance(ctx, parentID)
		if err != nil {
			return nil, err
		}
		if defKey == "" {
			defKey = parent.DefinitionKey
		} else if defKey != parent.DefinitionKey {
			return nil, fmt.Errorf("%w: definitionKey does not match parent process", domain.ErrInvalid)
		}
	}
	if defKey == "" {
		return nil, fmt.Errorf("%w: definitionKey is required", domain.ErrInvalid)
	}
	def, err := e.store.GetDefinitionByKey(ctx, domain.TenantID(ctx), defKey)
	if err != nil {
		return nil, fmt.Errorf("%w: unknown definition %s (start the process first)", err, defKey)
	}

	kind := in.ToKind
	ids := uniqueIDs(in.ToIDs)
	if in.ToID != "" {
		ids = uniqueIDs(append(ids, in.ToID))
	}
	switch kind {
	case domain.AssigneeUser:
		if len(ids) != 1 {
			return nil, fmt.Errorf("%w: user referral needs exactly one id", domain.ErrInvalid)
		}
	case domain.AssigneeGroup:
		if len(ids) != 1 {
			return nil, fmt.Errorf("%w: group referral needs exactly one id", domain.ErrInvalid)
		}
		members, err := e.dir.GroupMembers(ctx, ids[0])
		if err != nil {
			return nil, err
		}
		if len(members) == 0 {
			return nil, domain.ErrEmptyGroup
		}
	case domain.AssigneeUsers:
		if len(ids) == 0 {
			return nil, fmt.Errorf("%w: users referral needs at least one id", domain.ErrInvalid)
		}
		kind = domain.AssigneeUser
	default:
		return nil, fmt.Errorf("%w: to.kind must be user, group, or users", domain.ErrInvalid)
	}

	now := e.now()
	inst := &domain.ProcessInstance{
		ID:               domain.NewID(),
		TenantID:         domain.TenantID(ctx),
		DefinitionID:     def.ID,
		DefinitionKey:    def.Key,
		ParentInstanceID: parentID,
		Status:           domain.InstanceRunning,
		Parameters:       in.Parameters.Clone(),
		StartedBy:        actor,
		CreatedAt:        now,
		UpdatedAt:        now,
	}
	if err := e.store.CreateInstance(ctx, inst); err != nil {
		return nil, err
	}

	tasks := make([]*domain.Task, 0, len(ids))
	for _, id := range ids {
		t := &domain.Task{
			ID:               domain.NewID(),
			TenantID:         domain.TenantID(ctx),
			InstanceID:       inst.ID,
			ParentInstanceID: parentID,
			DefinitionKey:    def.Key,
			Title:            in.Title,
			AssigneeKind:     kind,
			AssigneeID:       id,
			AssignedBy:       actor,
			Status:           domain.TaskOpen,
			CreatedAt:        now,
			UpdatedAt:        now,
		}
		if err := e.store.SaveTask(ctx, t); err != nil {
			return nil, err
		}
		tasks = append(tasks, t)
	}
	out := &domain.ReferResult{
		InstanceID:    inst.ID,
		DefinitionKey: def.Key,
		Tasks:         tasks,
	}
	if len(tasks) == 1 {
		out.Task = tasks[0]
	}
	return out, nil
}

func uniqueIDs(ids []string) []string {
	seen := make(map[string]struct{}, len(ids))
	out := make([]string, 0, len(ids))
	for _, id := range ids {
		id = strings.TrimSpace(id)
		if id == "" {
			continue
		}
		if _, ok := seen[id]; ok {
			continue
		}
		seen[id] = struct{}{}
		out = append(out, id)
	}
	return out
}

func (e *Engine) PendingTasks(ctx context.Context, userID, groupID string) ([]*domain.Task, error) {
	userID = strings.TrimSpace(userID)
	groupID = strings.TrimSpace(groupID)
	if userID == "" && groupID == "" {
		return nil, fmt.Errorf("%w: user or group is required", domain.ErrInvalid)
	}
	filter := domain.TaskFilter{
		Status:   domain.TaskOpen,
		TenantID: domain.TenantID(ctx),
	}
	if groupID != "" {
		filter.Status = ""
		filter.GroupID = groupID
		filter.Statuses = []domain.TaskStatus{domain.TaskOpen, domain.TaskClaimed}
		tasks, err := e.store.ListTasks(ctx, filter)
		if err != nil {
			return nil, err
		}
		if tasks == nil {
			tasks = []*domain.Task{}
		}
		return tasks, nil
	}
	filter.UserID = userID
	filter.Status = domain.TaskOpen
	groups, err := e.dir.UserGroups(ctx, userID)
	if err != nil {
		return nil, err
	}
	filter.GroupIDs = groups
	open, err := e.store.ListTasks(ctx, filter)
	if err != nil {
		return nil, err
	}
	claimed, err := e.store.ListTasks(ctx, domain.TaskFilter{
		ClaimedBy: userID,
		Status:    domain.TaskClaimed,
		TenantID:  domain.TenantID(ctx),
	})
	if err != nil {
		return nil, err
	}
	return mergeTasks(open, claimed), nil
}

func mergeTasks(parts ...[]*domain.Task) []*domain.Task {
	seen := make(map[string]struct{})
	out := make([]*domain.Task, 0)
	for _, list := range parts {
		for _, t := range list {
			if t == nil {
				continue
			}
			if _, ok := seen[t.ID]; ok {
				continue
			}
			seen[t.ID] = struct{}{}
			out = append(out, t)
		}
	}
	return out
}

func (e *Engine) GetTask(ctx context.Context, id string) (*domain.Task, error) {
	t, err := e.store.GetTask(ctx, id)
	if err != nil {
		return nil, err
	}
	if domain.NormalizeTenant(t.TenantID) != domain.TenantID(ctx) {
		return nil, domain.ErrForbiddenTenant
	}
	return t, nil
}

func (e *Engine) ListTasksByInstance(ctx context.Context, instanceID string) ([]*domain.Task, error) {
	if _, err := e.GetInstance(ctx, instanceID); err != nil {
		return nil, err
	}
	tasks, err := e.store.ListTasks(ctx, domain.TaskFilter{
		InstanceID: instanceID,
		TenantID:   domain.TenantID(ctx),
	})
	if err != nil {
		return nil, err
	}
	if tasks == nil {
		tasks = []*domain.Task{}
	}
	return tasks, nil
}

func (e *Engine) Completion(ctx context.Context, instanceID string) (*domain.Completion, error) {
	if _, err := e.GetInstance(ctx, instanceID); err != nil {
		return nil, err
	}
	tasks, err := e.store.ListTasks(ctx, domain.TaskFilter{
		InstanceID: instanceID,
		TenantID:   domain.TenantID(ctx),
	})
	if err != nil {
		return nil, err
	}
	return completionOf(instanceID, tasks), nil
}

func completionOf(instanceID string, tasks []*domain.Task) *domain.Completion {
	if tasks == nil {
		tasks = []*domain.Task{}
	}
	owned := make([]*domain.Task, 0, len(tasks))
	for _, t := range tasks {
		if t.InstanceID == instanceID {
			owned = append(owned, t)
		}
	}
	c := &domain.Completion{InstanceID: instanceID, Tasks: owned, Total: len(owned)}
	for _, t := range owned {
		if t.Status == domain.TaskDone {
			c.Completed++
		} else {
			c.Open++
		}
	}
	c.AllCompleted = c.Total > 0 && c.Open == 0 && c.Completed == c.Total
	return c
}

func (e *Engine) ClaimTask(ctx context.Context, taskID, actor string) (*domain.Task, error) {
	actor = strings.TrimSpace(actor)
	if actor == "" {
		return nil, fmt.Errorf("%w: actor is required", domain.ErrInvalid)
	}
	task, err := e.GetTask(ctx, taskID)
	if err != nil {
		return nil, err
	}
	if err := e.canAct(ctx, task, actor); err != nil {
		return nil, err
	}
	if task.Status == domain.TaskClaimed {
		return nil, domain.ErrAlreadyClaimed
	}
	now := e.now()
	return e.store.TransitionTask(ctx, taskID, []domain.TaskStatus{domain.TaskOpen}, func(t *domain.Task) error {
		t.Status = domain.TaskClaimed
		t.ClaimedBy = actor
		t.UpdatedAt = now
		return nil
	})
}

func (e *Engine) UnclaimTask(ctx context.Context, taskID, actor string) (*domain.Task, error) {
	actor = strings.TrimSpace(actor)
	if actor == "" {
		return nil, fmt.Errorf("%w: actor is required", domain.ErrInvalid)
	}
	task, err := e.GetTask(ctx, taskID)
	if err != nil {
		return nil, err
	}
	if task.Status != domain.TaskClaimed {
		return nil, domain.ErrNotClaimed
	}
	if task.ClaimedBy != actor {
		return nil, domain.ErrForbidden
	}
	now := e.now()
	return e.store.TransitionTask(ctx, taskID, []domain.TaskStatus{domain.TaskClaimed}, func(t *domain.Task) error {
		t.Status = domain.TaskOpen
		t.ClaimedBy = ""
		t.UpdatedAt = now
		return nil
	})
}

func (e *Engine) CompleteTask(ctx context.Context, taskID, actor, note string, params domain.Vars) (*domain.CompleteResult, error) {
	actor = strings.TrimSpace(actor)
	if actor == "" {
		return nil, fmt.Errorf("%w: actor is required", domain.ErrInvalid)
	}
	task, err := e.GetTask(ctx, taskID)
	if err != nil {
		return nil, err
	}
	if err := e.canComplete(ctx, task, actor); err != nil {
		return nil, err
	}
	now := e.now()
	updated, err := e.store.TransitionTask(ctx, taskID, []domain.TaskStatus{domain.TaskOpen, domain.TaskClaimed}, func(t *domain.Task) error {
		if t.AssigneeKind == domain.AssigneeGroup && t.ClaimedBy != actor {
			return domain.ErrNotClaimed
		}
		t.Status = domain.TaskDone
		t.Note = note
		if t.ClaimedBy == "" {
			t.ClaimedBy = actor
		}
		t.UpdatedAt = now
		t.CompletedAt = &now
		return nil
	})
	if err != nil {
		return nil, err
	}
	inst, err := e.GetInstance(ctx, updated.InstanceID)
	if err != nil {
		return nil, err
	}
	if len(params) > 0 {
		inst.Parameters = inst.Parameters.Merge(params)
		inst.UpdatedAt = now
		if err := e.store.UpdateInstance(ctx, inst); err != nil {
			return nil, err
		}
	}
	comp, err := e.Completion(ctx, updated.InstanceID)
	if err != nil {
		return nil, err
	}
	if comp.AllCompleted && inst.Status != domain.InstanceCompleted {
		inst.Status = domain.InstanceCompleted
		inst.UpdatedAt = now
		if err := e.store.UpdateInstance(ctx, inst); err != nil {
			return nil, err
		}
	}
	return &domain.CompleteResult{Task: updated, Completion: comp}, nil
}

func (e *Engine) canAct(ctx context.Context, task *domain.Task, actor string) error {
	switch task.AssigneeKind {
	case domain.AssigneeUser:
		if task.AssigneeID != actor {
			return domain.ErrForbidden
		}
	case domain.AssigneeGroup:
		ok, err := identity.IsMember(ctx, e.dir, actor, task.AssigneeID)
		if err != nil {
			return err
		}
		if !ok {
			return domain.ErrForbidden
		}
	default:
		return domain.ErrForbidden
	}
	return nil
}

func (e *Engine) canComplete(ctx context.Context, task *domain.Task, actor string) error {
	if err := e.canAct(ctx, task, actor); err != nil {
		return err
	}
	if task.AssigneeKind == domain.AssigneeGroup {
		if task.Status != domain.TaskClaimed || task.ClaimedBy != actor {
			return domain.ErrNotClaimed
		}
	}
	if task.Status == domain.TaskClaimed && task.ClaimedBy != actor {
		return domain.ErrForbidden
	}
	return nil
}
