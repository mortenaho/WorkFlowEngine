package domain

import "time"

type AssigneeKind string

const (
	AssigneeUser  AssigneeKind = "user"
	AssigneeGroup AssigneeKind = "group"
	AssigneeUsers AssigneeKind = "users"
)

type InstanceStatus string

const (
	InstanceRunning   InstanceStatus = "running"
	InstanceCompleted InstanceStatus = "completed"
)

type TaskStatus string

const (
	TaskOpen    TaskStatus = "open"
	TaskClaimed TaskStatus = "claimed"
	TaskDone    TaskStatus = "done"
)

type Vars map[string]any

func (v Vars) Clone() Vars {
	if v == nil {
		return Vars{}
	}
	out := make(Vars, len(v))
	for k, val := range v {
		out[k] = val
	}
	return out
}

func (v Vars) Merge(other Vars) Vars {
	out := v.Clone()
	for k, val := range other {
		out[k] = val
	}
	return out
}

// Definition is a registered process type (not a graph).
type Definition struct {
	ID        string    `json:"id"`
	TenantID  string    `json:"tenantId,omitempty"`
	Key       string    `json:"key"`
	Name      string    `json:"name,omitempty"`
	CreatedAt time.Time `json:"createdAt"`
}

type ProcessInstance struct {
	ID               string         `json:"id"`
	TenantID         string         `json:"tenantId,omitempty"`
	DefinitionID     string         `json:"definitionId"`
	DefinitionKey    string         `json:"definitionKey"`
	ParentInstanceID string         `json:"parentInstanceId,omitempty"`
	Status           InstanceStatus `json:"status"`
	Parameters       Vars           `json:"parameters,omitempty"`
	StartedBy        string         `json:"initiator"`
	CreatedAt        time.Time      `json:"createdAt"`
	UpdatedAt        time.Time      `json:"updatedAt"`
}

type Task struct {
	ID               string       `json:"id"`
	TenantID         string       `json:"tenantId,omitempty"`
	InstanceID       string       `json:"instanceId"`
	ParentInstanceID string       `json:"parentInstanceId,omitempty"`
	DefinitionKey    string       `json:"definitionKey"`
	Title            string       `json:"title,omitempty"`
	AssigneeKind     AssigneeKind `json:"assigneeKind"`
	AssigneeID       string       `json:"assigneeId"`
	AssignedBy       string       `json:"assignedBy"`
	ClaimedBy        string       `json:"claimedBy,omitempty"`
	Status           TaskStatus   `json:"status"`
	Note             string       `json:"note,omitempty"`
	CreatedAt        time.Time    `json:"createdAt"`
	UpdatedAt        time.Time    `json:"updatedAt"`
	CompletedAt      *time.Time   `json:"completedAt,omitempty"`
}

type TaskFilter struct {
	UserID     string
	GroupID    string
	GroupIDs   []string
	InstanceID string
	Status     TaskStatus
	Statuses   []TaskStatus
	ClaimedBy  string
	TenantID   string
}

type StartResult struct {
	DefinitionKey string `json:"definitionKey"`
	InstanceID    string `json:"instanceId"`
}

type ReferInput struct {
	DefinitionKey    string
	ParentInstanceID string
	Title            string
	Parameters       Vars
	ToKind           AssigneeKind
	ToID             string
	ToIDs            []string
}

type ReferResult struct {
	InstanceID    string  `json:"instanceId"`
	DefinitionKey string  `json:"definitionKey"`
	Task          *Task   `json:"task,omitempty"`
	Tasks         []*Task `json:"tasks"`
}

type Completion struct {
	InstanceID   string  `json:"instanceId"`
	AllCompleted bool    `json:"allCompleted"`
	Total        int     `json:"total"`
	Completed    int     `json:"completed"`
	Open         int     `json:"open"`
	Tasks        []*Task `json:"tasks"`
}

type CompleteResult struct {
	Task       *Task       `json:"task"`
	Completion *Completion `json:"completion"`
}

type ProcessInstanceDetail struct {
	InstanceID        string         `json:"instanceId"`
	ProcessKey        string         `json:"processKey"`
	DefinitionKey     string         `json:"definitionKey"`
	Initiator         string         `json:"initiator"`
	Status            InstanceStatus `json:"status"`
	Parameters        Vars           `json:"parameters,omitempty"`
	CreatedAt         time.Time      `json:"createdAt"`
	UpdatedAt         time.Time      `json:"updatedAt"`
	Tasks             []*Task        `json:"tasks"`
	TaskTotal         int            `json:"taskTotal"`
	TasksCompleted    int            `json:"tasksCompleted"`
	TasksOpen         int            `json:"tasksOpen"`
	AllTasksCompleted bool           `json:"allTasksCompleted"`
}

type ProcessList struct {
	ProcessKey string                   `json:"processKey"`
	Total      int                      `json:"total"`
	Instances  []*ProcessInstanceDetail `json:"instances"`
}
