package postgres

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgxpool"

	"github.com/mortenaho/workflowengine/internal/domain"
	"github.com/mortenaho/workflowengine/internal/store"
)

type Store struct {
	pool *pgxpool.Pool
}

func Open(ctx context.Context, dsn string) (*Store, error) {
	pool, err := pgxpool.New(ctx, dsn)
	if err != nil {
		return nil, err
	}
	s := &Store{pool: pool}
	if err := s.migrate(ctx); err != nil {
		pool.Close()
		return nil, err
	}
	return s, nil
}

func (s *Store) Close() {
	s.pool.Close()
}

var _ store.Store = (*Store)(nil)

const schema = `
CREATE TABLE IF NOT EXISTS definitions (
  id TEXT PRIMARY KEY,
  tenant_id TEXT NOT NULL DEFAULT 'default',
  key TEXT NOT NULL,
  name TEXT NOT NULL DEFAULT '',
  version INT NOT NULL DEFAULT 1,
  graph JSONB NOT NULL DEFAULT '{}'::jsonb,
  published BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE TABLE IF NOT EXISTS instances (
  id TEXT PRIMARY KEY,
  tenant_id TEXT NOT NULL DEFAULT 'default',
  definition_id TEXT NOT NULL,
  definition_key TEXT NOT NULL,
  parent_instance_id TEXT NOT NULL DEFAULT '',
  status TEXT NOT NULL,
  vars JSONB NOT NULL DEFAULT '{}'::jsonb,
  started_by TEXT NOT NULL,
  version INT NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL
);
CREATE TABLE IF NOT EXISTS tasks (
  id TEXT PRIMARY KEY,
  tenant_id TEXT NOT NULL DEFAULT 'default',
  instance_id TEXT NOT NULL,
  parent_instance_id TEXT NOT NULL DEFAULT '',
  definition_key TEXT NOT NULL DEFAULT '',
  node_id TEXT NOT NULL DEFAULT '',
  token_id TEXT NOT NULL DEFAULT '',
  title TEXT NOT NULL DEFAULT '',
  note TEXT NOT NULL DEFAULT '',
  assignee_kind TEXT NOT NULL,
  assignee_id TEXT NOT NULL,
  claimed_by TEXT NOT NULL DEFAULT '',
  assigned_by TEXT NOT NULL DEFAULT '',
  status TEXT NOT NULL,
  group_mode TEXT NOT NULL DEFAULT '',
  return_reason TEXT NOT NULL DEFAULT '',
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  completed_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS tasks_instance_idx ON tasks(instance_id);
CREATE INDEX IF NOT EXISTS tasks_assignee_idx ON tasks(assignee_kind, assignee_id, status);
CREATE INDEX IF NOT EXISTS tasks_parent_idx ON tasks(parent_instance_id);
`

const alters = `
ALTER TABLE definitions ADD COLUMN IF NOT EXISTS tenant_id TEXT NOT NULL DEFAULT 'default';
ALTER TABLE definitions ADD COLUMN IF NOT EXISTS name TEXT NOT NULL DEFAULT '';
ALTER TABLE definitions ADD COLUMN IF NOT EXISTS created_at TIMESTAMPTZ NOT NULL DEFAULT NOW();
ALTER TABLE instances ADD COLUMN IF NOT EXISTS tenant_id TEXT NOT NULL DEFAULT 'default';
ALTER TABLE instances ADD COLUMN IF NOT EXISTS parent_instance_id TEXT NOT NULL DEFAULT '';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS tenant_id TEXT NOT NULL DEFAULT 'default';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS parent_instance_id TEXT NOT NULL DEFAULT '';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS definition_key TEXT NOT NULL DEFAULT '';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS title TEXT NOT NULL DEFAULT '';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS note TEXT NOT NULL DEFAULT '';
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS completed_at TIMESTAMPTZ;
`

func (s *Store) migrate(ctx context.Context) error {
	if _, err := s.pool.Exec(ctx, schema); err != nil {
		return err
	}
	_, err := s.pool.Exec(ctx, alters)
	return err
}

func encodeVars(v domain.Vars) ([]byte, error) {
	if v == nil {
		v = domain.Vars{}
	}
	return json.Marshal(v)
}

func decodeVars(b []byte) domain.Vars {
	if len(b) == 0 {
		return domain.Vars{}
	}
	var v domain.Vars
	if err := json.Unmarshal(b, &v); err != nil || v == nil {
		return domain.Vars{}
	}
	return v
}

func (s *Store) SaveDefinition(ctx context.Context, def *domain.Definition) error {
	_, err := s.pool.Exec(ctx, `
INSERT INTO definitions (id, tenant_id, key, name, version, graph, published, created_at)
VALUES ($1,$2,$3,$4,1,'{}'::jsonb,TRUE,$5)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name`,
		def.ID, domain.NormalizeTenant(def.TenantID), def.Key, def.Name, def.CreatedAt)
	return err
}

func (s *Store) GetDefinition(ctx context.Context, id string) (*domain.Definition, error) {
	row := s.pool.QueryRow(ctx, `
SELECT id, tenant_id, key, name, created_at FROM definitions WHERE id=$1`, id)
	return scanDef(row)
}

func (s *Store) GetDefinitionByKey(ctx context.Context, tenantID, key string) (*domain.Definition, error) {
	row := s.pool.QueryRow(ctx, `
SELECT id, tenant_id, key, name, created_at
FROM definitions
WHERE tenant_id=$1 AND key=$2
ORDER BY created_at DESC
LIMIT 1`, domain.NormalizeTenant(tenantID), key)
	return scanDef(row)
}

type scanner interface {
	Scan(dest ...any) error
}

func scanDef(row scanner) (*domain.Definition, error) {
	var d domain.Definition
	err := row.Scan(&d.ID, &d.TenantID, &d.Key, &d.Name, &d.CreatedAt)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, domain.ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return &d, nil
}

func (s *Store) CreateInstance(ctx context.Context, inst *domain.ProcessInstance) error {
	raw, err := encodeVars(inst.Parameters)
	if err != nil {
		return err
	}
	_, err = s.pool.Exec(ctx, `
INSERT INTO instances (id, tenant_id, definition_id, definition_key, parent_instance_id, status, vars, started_by, version, created_at, updated_at)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,1,$9,$10)`,
		inst.ID, domain.NormalizeTenant(inst.TenantID), inst.DefinitionID, inst.DefinitionKey,
		inst.ParentInstanceID, inst.Status, raw, inst.StartedBy, inst.CreatedAt, inst.UpdatedAt)
	return err
}

func (s *Store) GetInstance(ctx context.Context, id string) (*domain.ProcessInstance, error) {
	row := s.pool.QueryRow(ctx, `
SELECT id, tenant_id, definition_id, definition_key, parent_instance_id, status, vars, started_by, created_at, updated_at
FROM instances WHERE id=$1`, id)
	return scanInst(row)
}

func (s *Store) UpdateInstance(ctx context.Context, inst *domain.ProcessInstance) error {
	raw, err := encodeVars(inst.Parameters)
	if err != nil {
		return err
	}
	tag, err := s.pool.Exec(ctx, `
UPDATE instances SET status=$2, vars=$3, updated_at=$4 WHERE id=$1`,
		inst.ID, inst.Status, raw, inst.UpdatedAt)
	if err != nil {
		return err
	}
	if tag.RowsAffected() == 0 {
		return domain.ErrNotFound
	}
	return nil
}

func scanInst(row scanner) (*domain.ProcessInstance, error) {
	var inst domain.ProcessInstance
	var raw []byte
	err := row.Scan(&inst.ID, &inst.TenantID, &inst.DefinitionID, &inst.DefinitionKey,
		&inst.ParentInstanceID, &inst.Status, &raw, &inst.StartedBy, &inst.CreatedAt, &inst.UpdatedAt)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, domain.ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	inst.Parameters = decodeVars(raw)
	return &inst, nil
}

func (s *Store) SaveTask(ctx context.Context, task *domain.Task) error {
	_, err := s.pool.Exec(ctx, `
INSERT INTO tasks (id, tenant_id, instance_id, parent_instance_id, definition_key, title, note,
  assignee_kind, assignee_id, assigned_by, claimed_by, status, created_at, updated_at, completed_at)
VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15)`,
		task.ID, domain.NormalizeTenant(task.TenantID), task.InstanceID, task.ParentInstanceID,
		task.DefinitionKey, task.Title, task.Note, task.AssigneeKind, task.AssigneeID,
		task.AssignedBy, task.ClaimedBy, task.Status, task.CreatedAt, task.UpdatedAt, task.CompletedAt)
	return err
}

func (s *Store) GetTask(ctx context.Context, id string) (*domain.Task, error) {
	row := s.pool.QueryRow(ctx, taskSelect+` WHERE id=$1`, id)
	return scanTask(row)
}

func (s *Store) TransitionTask(ctx context.Context, id string, allowed []domain.TaskStatus, apply func(*domain.Task) error) (*domain.Task, error) {
	tx, err := s.pool.Begin(ctx)
	if err != nil {
		return nil, err
	}
	defer func() { _ = tx.Rollback(ctx) }()

	row := tx.QueryRow(ctx, taskSelect+` WHERE id=$1 FOR UPDATE`, id)
	task, err := scanTask(row)
	if err != nil {
		return nil, err
	}
	ok := false
	for _, st := range allowed {
		if task.Status == st {
			ok = true
			break
		}
	}
	if !ok {
		return nil, domain.ErrNotOpen
	}
	if err := apply(task); err != nil {
		return nil, err
	}
	_, err = tx.Exec(ctx, `
UPDATE tasks SET status=$2, note=$3, updated_at=$4, completed_at=$5, claimed_by=$6 WHERE id=$1`,
		task.ID, task.Status, task.Note, task.UpdatedAt, task.CompletedAt, task.ClaimedBy)
	if err != nil {
		return nil, err
	}
	if err := tx.Commit(ctx); err != nil {
		return nil, err
	}
	return task, nil
}

const taskSelect = `
SELECT id, tenant_id, instance_id, parent_instance_id, definition_key, title, note,
  assignee_kind, assignee_id, assigned_by, claimed_by, status, created_at, updated_at, completed_at
FROM tasks`

func scanTask(row scanner) (*domain.Task, error) {
	var t domain.Task
	err := row.Scan(&t.ID, &t.TenantID, &t.InstanceID, &t.ParentInstanceID, &t.DefinitionKey,
		&t.Title, &t.Note, &t.AssigneeKind, &t.AssigneeID, &t.AssignedBy, &t.ClaimedBy, &t.Status,
		&t.CreatedAt, &t.UpdatedAt, &t.CompletedAt)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, domain.ErrNotFound
	}
	if err != nil {
		return nil, err
	}
	return &t, nil
}

func (s *Store) ListTasks(ctx context.Context, f domain.TaskFilter) ([]*domain.Task, error) {
	q := taskSelect + ` WHERE 1=1`
	args := []any{}
	i := 1
	if f.TenantID != "" {
		q += fmt.Sprintf(` AND tenant_id=$%d`, i)
		args = append(args, domain.NormalizeTenant(f.TenantID))
		i++
	}
	if f.InstanceID != "" {
		q += fmt.Sprintf(` AND (instance_id=$%d OR parent_instance_id=$%d)`, i, i)
		args = append(args, f.InstanceID)
		i++
	}
	if f.Status != "" {
		q += fmt.Sprintf(` AND status=$%d`, i)
		args = append(args, f.Status)
		i++
	}
	if len(f.Statuses) > 0 {
		sts := make([]string, len(f.Statuses))
		for i, st := range f.Statuses {
			sts[i] = string(st)
		}
		q += fmt.Sprintf(` AND status = ANY($%d)`, i)
		args = append(args, sts)
		i++
	}
	if f.ClaimedBy != "" {
		q += fmt.Sprintf(` AND claimed_by=$%d`, i)
		args = append(args, f.ClaimedBy)
		i++
	}
	switch {
	case f.GroupID != "":
		q += fmt.Sprintf(` AND assignee_kind='group' AND assignee_id=$%d`, i)
		args = append(args, f.GroupID)
	case f.UserID != "":
		if len(f.GroupIDs) == 0 {
			q += fmt.Sprintf(` AND assignee_kind='user' AND assignee_id=$%d`, i)
			args = append(args, f.UserID)
		} else {
			q += fmt.Sprintf(` AND ((assignee_kind='user' AND assignee_id=$%d) OR (assignee_kind='group' AND assignee_id = ANY($%d)))`, i, i+1)
			args = append(args, f.UserID, f.GroupIDs)
		}
	}
	rows, err := s.pool.Query(ctx, q+` ORDER BY created_at`, args...)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	out := make([]*domain.Task, 0)
	for rows.Next() {
		t, err := scanTask(rows)
		if err != nil {
			return nil, err
		}
		out = append(out, t)
	}
	return out, rows.Err()
}
