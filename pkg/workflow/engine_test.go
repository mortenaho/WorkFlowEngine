package workflow_test

import (
	"context"
	"testing"

	"github.com/mortenaho/workflowengine/pkg/workflow"
)

func testEngine(t *testing.T) (*workflow.Engine, context.Context) {
	t.Helper()
	dir := workflow.NewStaticDirectory(
		[]string{"alice", "bob", "cara", "dan"},
		map[string][]string{
			"legal":   {"bob", "cara"},
			"finance": {"dan", "cara"},
		},
	)
	return workflow.NewEngine(workflow.NewMemoryStore(), dir), context.Background()
}

func TestStartReturnsDefinitionKeyAndInstanceID(t *testing.T) {
	eng, ctx := testEngine(t)
	out, err := eng.Start(ctx, "purchase", "alice", workflow.Vars{"amount": 1e8})
	if err != nil {
		t.Fatal(err)
	}
	if out.DefinitionKey != "purchase" {
		t.Fatalf("definitionKey=%q", out.DefinitionKey)
	}
	if out.InstanceID == "" {
		t.Fatal("expected instanceId")
	}
	inst, err := eng.GetInstance(ctx, out.InstanceID)
	if err != nil {
		t.Fatal(err)
	}
	if inst.StartedBy != "alice" {
		t.Fatalf("initiator=%q", inst.StartedBy)
	}
}

func TestReferToPerson(t *testing.T) {
	eng, ctx := testEngine(t)
	started, err := eng.Start(ctx, "purchase", "alice", nil)
	if err != nil {
		t.Fatal(err)
	}
	ref, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey:    started.DefinitionKey,
		ParentInstanceID: started.InstanceID,
		Title:            "بررسی",
		ToKind:           workflow.AssigneeUser,
		ToID:             "bob",
	})
	if err != nil {
		t.Fatal(err)
	}
	if ref.InstanceID == "" || ref.InstanceID == started.InstanceID {
		t.Fatalf("expected new instanceId, got %q", ref.InstanceID)
	}
	if ref.Task == nil || ref.Task.AssigneeID != "bob" || ref.Task.Status != workflow.TaskOpen {
		t.Fatalf("task=%+v", ref.Task)
	}
	if len(ref.Tasks) != 1 {
		t.Fatalf("tasks=%d", len(ref.Tasks))
	}
}

func TestReferToGroup(t *testing.T) {
	eng, ctx := testEngine(t)
	started, _ := eng.Start(ctx, "purchase", "alice", nil)
	ref, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey:    started.DefinitionKey,
		ParentInstanceID: started.InstanceID,
		ToKind:           workflow.AssigneeGroup,
		ToID:             "legal",
	})
	if err != nil {
		t.Fatal(err)
	}
	if ref.Task.AssigneeKind != workflow.AssigneeGroup || ref.Task.AssigneeID != "legal" {
		t.Fatalf("task=%+v", ref.Task)
	}
	bob, err := eng.PendingTasks(ctx, "bob", "")
	if err != nil {
		t.Fatal(err)
	}
	if len(bob) != 1 {
		t.Fatalf("bob inbox=%d", len(bob))
	}
	cara, _ := eng.PendingTasks(ctx, "cara", "")
	if len(cara) != 1 {
		t.Fatalf("cara inbox=%d", len(cara))
	}
	group, _ := eng.PendingTasks(ctx, "", "legal")
	if len(group) != 1 {
		t.Fatalf("legal inbox=%d", len(group))
	}
	if _, err := eng.CompleteTask(ctx, ref.Task.ID, "bob", "ok", nil); err == nil {
		t.Fatal("group complete requires claim")
	}
	claimed, err := eng.ClaimTask(ctx, ref.Task.ID, "bob")
	if err != nil {
		t.Fatal(err)
	}
	if claimed.Status != workflow.TaskClaimed || claimed.ClaimedBy != "bob" {
		t.Fatalf("%+v", claimed)
	}
	if _, err := eng.ClaimTask(ctx, ref.Task.ID, "cara"); err == nil {
		t.Fatal("expected already claimed")
	}
	caraInbox, _ := eng.PendingTasks(ctx, "cara", "")
	if len(caraInbox) != 0 {
		t.Fatalf("cara should not see claimed task, got %d", len(caraInbox))
	}
	bobInbox, _ := eng.PendingTasks(ctx, "bob", "")
	if len(bobInbox) != 1 {
		t.Fatalf("bob should keep claimed task, got %d", len(bobInbox))
	}
	if _, err := eng.CompleteTask(ctx, ref.Task.ID, "bob", "ok", nil); err != nil {
		t.Fatal(err)
	}
}

func TestUnclaimReturnsToGroup(t *testing.T) {
	eng, ctx := testEngine(t)
	started, _ := eng.Start(ctx, "purchase", "alice", nil)
	ref, _ := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: started.DefinitionKey, ToKind: workflow.AssigneeGroup, ToID: "legal",
	})
	if _, err := eng.ClaimTask(ctx, ref.Task.ID, "bob"); err != nil {
		t.Fatal(err)
	}
	if _, err := eng.UnclaimTask(ctx, ref.Task.ID, "bob"); err != nil {
		t.Fatal(err)
	}
	claimed, err := eng.ClaimTask(ctx, ref.Task.ID, "cara")
	if err != nil {
		t.Fatal(err)
	}
	if claimed.ClaimedBy != "cara" {
		t.Fatalf("%+v", claimed)
	}
}

func TestPendingTasksByUserAndGroup(t *testing.T) {
	eng, ctx := testEngine(t)
	started, _ := eng.Start(ctx, "purchase", "alice", nil)
	_, _ = eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: started.DefinitionKey, ParentInstanceID: started.InstanceID,
		ToKind: workflow.AssigneeUser, ToID: "bob",
	})
	_, _ = eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: started.DefinitionKey, ParentInstanceID: started.InstanceID,
		ToKind: workflow.AssigneeGroup, ToID: "finance",
	})
	bob, err := eng.PendingTasks(ctx, "bob", "")
	if err != nil {
		t.Fatal(err)
	}
	if len(bob) != 1 {
		t.Fatalf("bob should only see personal task, got %d", len(bob))
	}
	dan, _ := eng.PendingTasks(ctx, "dan", "")
	if len(dan) != 1 {
		t.Fatalf("dan should see finance group task, got %d", len(dan))
	}
	finance, _ := eng.PendingTasks(ctx, "", "finance")
	if len(finance) != 1 || finance[0].AssigneeKind != workflow.AssigneeGroup {
		t.Fatalf("finance group=%+v", finance)
	}
}

func TestMultiUserAllCompleted(t *testing.T) {
	eng, ctx := testEngine(t)
	started, _ := eng.Start(ctx, "purchase", "alice", nil)
	ref, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey:    started.DefinitionKey,
		ParentInstanceID: started.InstanceID,
		ToKind:           workflow.AssigneeUsers,
		ToIDs:            []string{"bob", "cara", "dan"},
	})
	if err != nil {
		t.Fatal(err)
	}
	if len(ref.Tasks) != 3 {
		t.Fatalf("tasks=%d", len(ref.Tasks))
	}
	comp, err := eng.Completion(ctx, ref.InstanceID)
	if err != nil {
		t.Fatal(err)
	}
	if comp.AllCompleted || comp.Total != 3 || comp.Open != 3 {
		t.Fatalf("before=%+v", comp)
	}

	byUser := map[string]string{}
	for _, tk := range ref.Tasks {
		byUser[tk.AssigneeID] = tk.ID
	}
	out, err := eng.CompleteTask(ctx, byUser["bob"], "bob", "", nil)
	if err != nil {
		t.Fatal(err)
	}
	if out.Completion.AllCompleted || out.Completion.Completed != 1 {
		t.Fatalf("after bob=%+v", out.Completion)
	}
	if _, err := eng.CompleteTask(ctx, byUser["cara"], "cara", "", nil); err != nil {
		t.Fatal(err)
	}
	last, err := eng.CompleteTask(ctx, byUser["dan"], "dan", "", nil)
	if err != nil {
		t.Fatal(err)
	}
	if !last.Completion.AllCompleted || last.Completion.Completed != 3 || last.Completion.Open != 0 {
		t.Fatalf("after all=%+v", last.Completion)
	}
	inst, _ := eng.GetInstance(ctx, ref.InstanceID)
	if inst.Status != workflow.InstanceCompleted {
		t.Fatalf("status=%s", inst.Status)
	}
}

func TestStartRequiresFields(t *testing.T) {
	eng, ctx := testEngine(t)
	if _, err := eng.Start(ctx, "", "alice", nil); err == nil {
		t.Fatal("expected error")
	}
	if _, err := eng.Start(ctx, "purchase", "", nil); err == nil {
		t.Fatal("expected error")
	}
}

func TestCompleteForbiddenForOtherUser(t *testing.T) {
	eng, ctx := testEngine(t)
	started, _ := eng.Start(ctx, "purchase", "alice", nil)
	ref, _ := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: started.DefinitionKey, ToKind: workflow.AssigneeUser, ToID: "bob",
	})
	if _, err := eng.CompleteTask(ctx, ref.Task.ID, "cara", "", nil); err == nil {
		t.Fatal("expected forbidden")
	}
}
