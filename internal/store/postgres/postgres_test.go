package postgres

import (
	"context"
	"os"
	"testing"

	"github.com/mortenaho/workflowengine/internal/domain"
	"github.com/mortenaho/workflowengine/internal/engine"
	"github.com/mortenaho/workflowengine/internal/identity"
)

func TestPostgresStartReferComplete(t *testing.T) {
	dsn := os.Getenv("DATABASE_URL")
	if dsn == "" {
		t.Skip("DATABASE_URL not set")
	}
	ctx := context.Background()
	s, err := Open(ctx, dsn)
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(s.Close)
	dir := identity.NewStaticDirectory([]string{"alice", "bob"}, map[string][]string{"legal": {"bob"}})
	eng := engine.New(s, dir)
	ctx = domain.WithTenant(ctx, "test-"+domain.NewID()[:8])
	started, err := eng.Start(ctx, "purchase-pg", "alice", domain.Vars{"n": 1})
	if err != nil {
		t.Fatal(err)
	}
	ref, err := eng.Refer(ctx, "alice", domain.ReferInput{
		DefinitionKey: started.DefinitionKey, ParentInstanceID: started.InstanceID,
		ToKind: domain.AssigneeUsers, ToIDs: []string{"alice", "bob"},
	})
	if err != nil {
		t.Fatal(err)
	}
	if _, err := eng.CompleteTask(ctx, ref.Tasks[0].ID, ref.Tasks[0].AssigneeID, "", nil); err != nil {
		t.Fatal(err)
	}
	last, err := eng.CompleteTask(ctx, ref.Tasks[1].ID, ref.Tasks[1].AssigneeID, "", nil)
	if err != nil {
		t.Fatal(err)
	}
	if !last.Completion.AllCompleted {
		t.Fatalf("%+v", last.Completion)
	}
}
