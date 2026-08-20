package store

import (
	"context"

	"github.com/mortenaho/workflowengine/internal/domain"
)

type Store interface {
	SaveDefinition(ctx context.Context, def *domain.Definition) error
	GetDefinition(ctx context.Context, id string) (*domain.Definition, error)
	GetDefinitionByKey(ctx context.Context, tenantID, key string) (*domain.Definition, error)

	CreateInstance(ctx context.Context, inst *domain.ProcessInstance) error
	GetInstance(ctx context.Context, id string) (*domain.ProcessInstance, error)
	UpdateInstance(ctx context.Context, inst *domain.ProcessInstance) error

	SaveTask(ctx context.Context, task *domain.Task) error
	GetTask(ctx context.Context, id string) (*domain.Task, error)
	TransitionTask(ctx context.Context, id string, allowed []domain.TaskStatus, apply func(*domain.Task) error) (*domain.Task, error)
	ListTasks(ctx context.Context, filter domain.TaskFilter) ([]*domain.Task, error)
}
