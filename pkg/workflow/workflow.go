package workflow

import (
	"context"
	"time"

	"github.com/mortenaho/workflowengine/internal/domain"
	"github.com/mortenaho/workflowengine/internal/engine"
	"github.com/mortenaho/workflowengine/internal/identity"
	"github.com/mortenaho/workflowengine/internal/store"
	"github.com/mortenaho/workflowengine/internal/store/memory"
)

type (
	AssigneeKind   = domain.AssigneeKind
	InstanceStatus = domain.InstanceStatus
	Vars           = domain.Vars
	Definition     = domain.Definition
	Instance       = domain.ProcessInstance
	TaskStatus     = domain.TaskStatus
	Task           = domain.Task
	TaskFilter     = domain.TaskFilter
	StartResult    = domain.StartResult
	ReferInput     = domain.ReferInput
	ReferResult    = domain.ReferResult
	Completion     = domain.Completion
	CompleteResult = domain.CompleteResult
	Store          = store.Store
	Directory      = identity.Directory
	EngineOption   = engine.Option
)

const (
	AssigneeUser  = domain.AssigneeUser
	AssigneeGroup = domain.AssigneeGroup
	AssigneeUsers = domain.AssigneeUsers

	InstanceRunning   = domain.InstanceRunning
	InstanceCompleted = domain.InstanceCompleted

	TaskOpen    = domain.TaskOpen
	TaskClaimed = domain.TaskClaimed
	TaskDone    = domain.TaskDone
)

var (
	ErrNotFound        = domain.ErrNotFound
	ErrConflict        = domain.ErrConflict
	ErrForbidden       = domain.ErrForbidden
	ErrInvalid         = domain.ErrInvalid
	ErrNotOpen         = domain.ErrNotOpen
	ErrAlreadyClaimed  = domain.ErrAlreadyClaimed
	ErrNotClaimed      = domain.ErrNotClaimed
	ErrEmptyGroup      = domain.ErrEmptyGroup
	ErrUnauthorized    = domain.ErrUnauthorized
	ErrForbiddenTenant = domain.ErrForbiddenTenant
)

func NewMemoryStore() Store {
	return memory.New()
}

func NewStaticDirectory(users []string, groups map[string][]string) *identity.StaticDirectory {
	return identity.NewStaticDirectory(users, groups)
}

type Engine struct {
	inner *engine.Engine
}

func NewEngine(s Store, d Directory, opts ...EngineOption) *Engine {
	return &Engine{inner: engine.New(s, d, opts...)}
}

func WithClock(clock func() time.Time) EngineOption {
	return engine.WithClock(clock)
}

func WithTenant(ctx context.Context, tenantID string) context.Context {
	return domain.WithTenant(ctx, tenantID)
}

func (e *Engine) Register(ctx context.Context, key, name string) (*Definition, error) {
	return e.inner.Register(ctx, key, name)
}

func (e *Engine) LatestDefinition(ctx context.Context, key string) (*Definition, error) {
	return e.inner.GetDefinitionByKey(ctx, key)
}

func (e *Engine) Start(ctx context.Context, processKey, initiator string, params Vars) (*StartResult, error) {
	return e.inner.Start(ctx, processKey, initiator, params)
}

func (e *Engine) Refer(ctx context.Context, actor string, in ReferInput) (*ReferResult, error) {
	return e.inner.Refer(ctx, actor, in)
}

func (e *Engine) PendingTasks(ctx context.Context, userID, groupID string) ([]*Task, error) {
	return e.inner.PendingTasks(ctx, userID, groupID)
}

func (e *Engine) ClaimTask(ctx context.Context, taskID, actor string) (*Task, error) {
	return e.inner.ClaimTask(ctx, taskID, actor)
}

func (e *Engine) UnclaimTask(ctx context.Context, taskID, actor string) (*Task, error) {
	return e.inner.UnclaimTask(ctx, taskID, actor)
}

func (e *Engine) Completion(ctx context.Context, instanceID string) (*Completion, error) {
	return e.inner.Completion(ctx, instanceID)
}

func (e *Engine) CompleteTask(ctx context.Context, taskID, actor, note string, params Vars) (*CompleteResult, error) {
	return e.inner.CompleteTask(ctx, taskID, actor, note, params)
}

func (e *Engine) GetInstance(ctx context.Context, id string) (*Instance, error) {
	return e.inner.GetInstance(ctx, id)
}

func (e *Engine) GetTask(ctx context.Context, id string) (*Task, error) {
	return e.inner.GetTask(ctx, id)
}

func (e *Engine) ListTasksByInstance(ctx context.Context, instanceID string) ([]*Task, error) {
	return e.inner.ListTasksByInstance(ctx, instanceID)
}
