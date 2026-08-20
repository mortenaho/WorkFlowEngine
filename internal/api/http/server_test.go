package httpapi

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"testing"

	"github.com/mortenaho/workflowengine/pkg/workflow"
)

func testServer(t *testing.T) *Server {
	t.Helper()
	dir := workflow.NewStaticDirectory(
		[]string{"alice", "bob", "cara", "dan"},
		map[string][]string{"legal": {"bob", "cara"}, "finance": {"dan", "cara"}},
	)
	return New(workflow.NewEngine(workflow.NewMemoryStore(), dir))
}

func doJSON(t *testing.T, h http.Handler, method, path, actor string, body any) *httptest.ResponseRecorder {
	t.Helper()
	var r *http.Request
	if body != nil {
		raw, err := json.Marshal(body)
		if err != nil {
			t.Fatal(err)
		}
		r = httptest.NewRequest(method, path, bytes.NewReader(raw))
		r.Header.Set("Content-Type", "application/json")
	} else {
		r = httptest.NewRequest(method, path, nil)
	}
	r.Header.Set("X-Actor-Id", actor)
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	return w
}

func TestHTTPStartReferInboxComplete(t *testing.T) {
	s := testServer(t)
	h := s.Handler()

	w := doJSON(t, h, http.MethodPost, "/v1/processes/start", "alice", map[string]any{
		"processKey": "purchase",
		"initiator":  "alice",
		"parameters": map[string]any{"amount": 10},
	})
	if w.Code != http.StatusCreated {
		t.Fatalf("start %d %s", w.Code, w.Body.String())
	}
	var started workflow.StartResult
	if err := json.Unmarshal(w.Body.Bytes(), &started); err != nil {
		t.Fatal(err)
	}
	if started.DefinitionKey != "purchase" || started.InstanceID == "" {
		t.Fatalf("%+v", started)
	}

	w = doJSON(t, h, http.MethodPost, "/v1/referrals", "alice", map[string]any{
		"definitionKey":    started.DefinitionKey,
		"parentInstanceId": started.InstanceID,
		"title":            "بررسی",
		"to":               map[string]any{"kind": "users", "ids": []string{"bob", "cara"}},
	})
	if w.Code != http.StatusCreated {
		t.Fatalf("refer %d %s", w.Code, w.Body.String())
	}
	var ref workflow.ReferResult
	if err := json.Unmarshal(w.Body.Bytes(), &ref); err != nil {
		t.Fatal(err)
	}
	if ref.InstanceID == "" || len(ref.Tasks) != 2 {
		t.Fatalf("%+v", ref)
	}

	w = doJSON(t, h, http.MethodGet, "/v1/tasks?user=bob", "bob", nil)
	if w.Code != http.StatusOK {
		t.Fatalf("inbox %d %s", w.Code, w.Body.String())
	}
	var inbox []*workflow.Task
	if err := json.Unmarshal(w.Body.Bytes(), &inbox); err != nil {
		t.Fatal(err)
	}
	if len(inbox) != 1 {
		t.Fatalf("bob inbox=%d", len(inbox))
	}

	w = doJSON(t, h, http.MethodGet, "/v1/instances/"+ref.InstanceID+"/completion", "alice", nil)
	var comp workflow.Completion
	if err := json.Unmarshal(w.Body.Bytes(), &comp); err != nil {
		t.Fatal(err)
	}
	if comp.AllCompleted || comp.Total != 2 {
		t.Fatalf("%+v", comp)
	}

	bobTask, caraTask := "", ""
	for _, tk := range ref.Tasks {
		if tk.AssigneeID == "bob" {
			bobTask = tk.ID
		} else {
			caraTask = tk.ID
		}
	}
	w = doJSON(t, h, http.MethodPost, "/v1/tasks/"+bobTask+"/complete", "bob", map[string]any{"note": "ok"})
	if w.Code != http.StatusOK {
		t.Fatalf("complete %d %s", w.Code, w.Body.String())
	}
	var done workflow.CompleteResult
	if err := json.Unmarshal(w.Body.Bytes(), &done); err != nil {
		t.Fatal(err)
	}
	if done.Completion.AllCompleted {
		t.Fatal("should not be all completed yet")
	}
	w = doJSON(t, h, http.MethodPost, "/v1/tasks/"+caraTask+"/complete", "cara", map[string]any{})
	if w.Code != http.StatusOK {
		t.Fatalf("complete cara %d %s", w.Code, w.Body.String())
	}
	if err := json.Unmarshal(w.Body.Bytes(), &done); err != nil {
		t.Fatal(err)
	}
	if !done.Completion.AllCompleted {
		t.Fatalf("expected allCompleted: %+v", done.Completion)
	}
}

func TestHealth(t *testing.T) {
	w := doJSON(t, testServer(t).Handler(), http.MethodGet, "/health", "", nil)
	if w.Code != http.StatusOK {
		t.Fatalf("%d", w.Code)
	}
}

func TestReferUsesFromInBody(t *testing.T) {
	s := testServer(t)
	h := s.Handler()
	w := doJSON(t, h, http.MethodPost, "/v1/processes/start", "", map[string]any{
		"processKey": "purchase",
		"initiator":  "alice",
	})
	if w.Code != http.StatusCreated {
		t.Fatalf("start %d %s", w.Code, w.Body.String())
	}
	var started workflow.StartResult
	if err := json.Unmarshal(w.Body.Bytes(), &started); err != nil {
		t.Fatal(err)
	}
	w = doJSON(t, h, http.MethodPost, "/v1/referrals", "", map[string]any{
		"definitionKey":    started.DefinitionKey,
		"parentInstanceId": started.InstanceID,
		"from":             "alice",
		"to":               map[string]any{"kind": "user", "id": "bob"},
	})
	if w.Code != http.StatusCreated {
		t.Fatalf("refer %d %s", w.Code, w.Body.String())
	}
	var ref workflow.ReferResult
	if err := json.Unmarshal(w.Body.Bytes(), &ref); err != nil {
		t.Fatal(err)
	}
	if ref.Task == nil || ref.Task.AssignedBy != "alice" {
		t.Fatalf("%+v", ref.Task)
	}
}
