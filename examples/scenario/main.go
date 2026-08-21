// سناریوی خاتمه همکاری: دو کارمند، ارجاع شخص/گروه/چندنفره، کلیم، کارتابل، تکمیل.
package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"os"

	"github.com/mortenaho/workflowengine/pkg/workflow"
)

func main() {
	if err := run(); err != nil {
		log.Fatal(err)
	}
}

func run() error {
	ctx := context.Background()
	eng := workflow.NewEngine(
		workflow.NewMemoryStore(),
		workflow.NewStaticDirectory(
			[]string{"alice", "bob", "cara", "dan"},
			map[string][]string{"legal": {"bob", "cara"}},
		),
	)

	step(1, "شروع فرایند برای کارمند ۱۰۰۱ (alice از منابع انسانی)")
	emp1, err := eng.Start(ctx, "employeeTermination", "alice", workflow.Vars{
		"employeeId": "1001", "employeeName": "رضا محمدی",
	})
	if err != nil {
		return err
	}
	dump(emp1)

	step(2, "شروع همان processKey برای کارمند ۱۰۰۲ — یک کلید، چند اجرا")
	emp2, err := eng.Start(ctx, "employeeTermination", "alice", workflow.Vars{
		"employeeId": "1002", "employeeName": "سارا احمدی",
	})
	if err != nil {
		return err
	}
	dump(emp2)

	step(3, "لیست همهٔ خاتمه‌همکاری‌ها با GET معادل processKey")
	list, err := eng.ListByProcessKey(ctx, "employeeTermination")
	if err != nil {
		return err
	}
	dump(list)

	step(4, "ارجاع بررسی حقوقی کارمند ۱۰۰۱ به شخص bob")
	legal, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey:    emp1.DefinitionKey,
		ParentInstanceID: emp1.InstanceID,
		Title:            "بررسی حقوقی خاتمه همکاری رضا",
		ToKind:           workflow.AssigneeUser,
		ToID:             "bob",
	})
	if err != nil {
		return err
	}
	dump(legal)

	step(5, "کارتابل bob — باید تسک حقوقی را ببیند")
	inbox, err := eng.PendingTasks(ctx, "bob", "")
	if err != nil {
		return err
	}
	dump(inbox)

	step(6, "ارجاع کارمند ۱۰۰۲ به گروه legal (bob و cara)")
	groupRef, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey:    emp2.DefinitionKey,
		ParentInstanceID: emp2.InstanceID,
		Title:            "بررسی گروهی سارا",
		ToKind:           workflow.AssigneeGroup,
		ToID:             "legal",
	})
	if err != nil {
		return err
	}
	dump(groupRef)

	step(7, "کارتابل cara قبل از کلیم — تسک گروهی را می‌بیند")
	caraBefore, err := eng.PendingTasks(ctx, "cara", "")
	if err != nil {
		return err
	}
	dump(caraBefore)

	step(8, "bob تسک گروهی را کلیم می‌کند")
	claimed, err := eng.ClaimTask(ctx, groupRef.Task.ID, "bob")
	if err != nil {
		return err
	}
	dump(claimed)

	step(9, "کارتابل cara بعد از کلیم — نباید تسک گروهی را ببیند")
	caraAfter, err := eng.PendingTasks(ctx, "cara", "")
	if err != nil {
		return err
	}
	dump(caraAfter)

	step(10, "bob تسک گروهی را complete می‌کند")
	groupDone, err := eng.CompleteTask(ctx, groupRef.Task.ID, "bob", "مدارک کامل است", nil)
	if err != nil {
		return err
	}
	dump(groupDone)

	step(11, "ارجاع موازی کارمند ۱۰۰۱ به bob و cara و dan")
	multi, err := eng.Refer(ctx, "alice", workflow.ReferInput{
		DefinitionKey:    emp1.DefinitionKey,
		ParentInstanceID: emp1.InstanceID,
		Title:            "تأیید نهایی رضا",
		ToKind:           workflow.AssigneeUsers,
		ToIDs:            []string{"bob", "cara", "dan"},
	})
	if err != nil {
		return err
	}
	dump(multi)

	step(12, "وضعیت تکمیل چندنفره — هنوز همه تمام نکرده‌اند")
	before, err := eng.Completion(ctx, multi.InstanceID)
	if err != nil {
		return err
	}
	dump(before)

	step(13, "هر سه نفر تسک را complete می‌کنند")
	for _, t := range multi.Tasks {
		out, err := eng.CompleteTask(ctx, t.ID, t.AssigneeID, "تأیید شد", workflow.Vars{"approved": true})
		if err != nil {
			return err
		}
		fmt.Printf("  %s → allCompleted=%v completed=%d/%d\n",
			t.AssigneeID, out.Completion.AllCompleted, out.Completion.Completed, out.Completion.Total)
	}

	step(14, "وضعیت نهایی چندنفره و لیست اجراهای employeeTermination")
	after, err := eng.Completion(ctx, multi.InstanceID)
	if err != nil {
		return err
	}
	dump(after)
	finalList, err := eng.ListByProcessKey(ctx, "employeeTermination")
	if err != nil {
		return err
	}
	dump(finalList)
	return nil
}

func step(n int, title string) {
	fmt.Fprintf(os.Stdout, "\n======== گام %d ========\n%s\n", n, title)
}

func dump(v any) {
	enc := json.NewEncoder(os.Stdout)
	enc.SetIndent("", "  ")
	enc.SetEscapeHTML(false)
	if err := enc.Encode(v); err != nil {
		log.Fatal(err)
	}
}
