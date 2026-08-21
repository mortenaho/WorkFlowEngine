package workflow_test

import (
	"testing"

	"github.com/mortenaho/workflowengine/pkg/workflow"
)

// سناریو: خاتمه همکاری دو کارمند با ارجاع شخص، گروه، چندنفره، کلیم و تکمیل.
func TestEmployeeTerminationScenario(t *testing.T) {
	eng, ctx := testEngine(t)

	emp1, err := eng.Start(ctx, "employeeTermination", "alice", workflow.Vars{
		"employeeId": "1001", "employeeName": "رضا محمدی",
	})
	if err != nil {
		t.Fatal(err)
	}
	emp2, err := eng.Start(ctx, "employeeTermination", "alice", workflow.Vars{
		"employeeId": "1002", "employeeName": "سارا احمدی",
	})
	if err != nil {
		t.Fatal(err)
	}

	list, err := eng.ListByProcessKey(ctx, "employeeTermination")
	if err != nil {
		t.Fatal(err)
	}
	if list.Total != 2 {
		t.Fatalf("expected 2 runs, got %d", list.Total)
	}

	legal, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: emp1.DefinitionKey, ParentInstanceID: emp1.InstanceID,
		Title: "بررسی حقوقی", ToKind: workflow.AssigneeUser, ToID: "bob",
	})
	if err != nil {
		t.Fatal(err)
	}
	bobInbox, _ := eng.PendingTasks(ctx, "bob", "")
	if len(bobInbox) != 1 {
		t.Fatalf("bob inbox=%d", len(bobInbox))
	}

	groupRef, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: emp2.DefinitionKey, ParentInstanceID: emp2.InstanceID,
		ToKind: workflow.AssigneeGroup, ToID: "legal",
	})
	if err != nil {
		t.Fatal(err)
	}
	if _, err := eng.CompleteTask(ctx, groupRef.Task.ID, "bob", "", nil); err == nil {
		t.Fatal("group complete must require claim")
	}
	if _, err := eng.ClaimTask(ctx, groupRef.Task.ID, "bob"); err != nil {
		t.Fatal(err)
	}
	caraInbox, _ := eng.PendingTasks(ctx, "cara", "")
	if len(caraInbox) != 0 {
		t.Fatalf("cara should not see claimed group task, got %d", len(caraInbox))
	}
	if _, err := eng.CompleteTask(ctx, groupRef.Task.ID, "bob", "ok", nil); err != nil {
		t.Fatal(err)
	}

	if _, err := eng.CompleteTask(ctx, legal.Task.ID, "bob", "ok", nil); err != nil {
		t.Fatal(err)
	}

	multi, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey: emp1.DefinitionKey, ParentInstanceID: emp1.InstanceID,
		ToKind: workflow.AssigneeUsers, ToIDs: []string{"bob", "cara", "dan"},
	})
	if err != nil {
		t.Fatal(err)
	}
	before, _ := eng.Completion(ctx, multi.InstanceID)
	if before.AllCompleted || before.Total != 3 {
		t.Fatalf("before=%+v", before)
	}
	var last *workflow.CompleteResult
	for _, tk := range multi.Tasks {
		last, err = eng.CompleteTask(ctx, tk.ID, tk.AssigneeID, "تأیید شد", nil)
		if err != nil {
			t.Fatal(err)
		}
	}
	if last == nil || !last.Completion.AllCompleted {
		t.Fatalf("expected allCompleted, got %+v", last)
	}

	final, _ := eng.ListByProcessKey(ctx, "employeeTermination")
	if final.Total != 2 {
		t.Fatalf("still 2 root runs, got %d", final.Total)
	}
}
