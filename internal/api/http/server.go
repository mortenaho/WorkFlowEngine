package httpapi

import (
	"encoding/json"
	"errors"
	"net/http"

	"github.com/mortenaho/workflowengine/internal/domain"
	"github.com/mortenaho/workflowengine/pkg/workflow"
)

type Server struct {
	engine  *workflow.Engine
	mux     *http.ServeMux
	apiKeys map[string]struct{}
}

func New(e *workflow.Engine, apiKeys ...string) *Server {
	s := &Server{engine: e, mux: http.NewServeMux(), apiKeys: parseAPIKeys(apiKeys)}
	s.routes()
	return s
}

func (s *Server) routes() {
	s.mux.HandleFunc("GET /health", s.health)
	s.mux.HandleFunc("POST /v1/definitions", s.register)
	s.mux.HandleFunc("GET /v1/definitions/{key}", s.getDefinition)
	s.mux.HandleFunc("POST /v1/processes/start", s.start)
	s.mux.HandleFunc("GET /v1/processes/{processKey}/instances", s.listByProcessKey)
	s.mux.HandleFunc("POST /v1/referrals", s.refer)
	s.mux.HandleFunc("GET /v1/tasks", s.pendingTasks)
	s.mux.HandleFunc("GET /v1/inbox", s.pendingTasks)
	s.mux.HandleFunc("GET /v1/tasks/{id}", s.getTask)
	s.mux.HandleFunc("POST /v1/tasks/{id}/claim", s.claim)
	s.mux.HandleFunc("POST /v1/tasks/{id}/unclaim", s.unclaim)
	s.mux.HandleFunc("POST /v1/tasks/{id}/complete", s.complete)
	s.mux.HandleFunc("GET /v1/instances/{id}", s.getInstance)
	s.mux.HandleFunc("GET /v1/instances/{id}/tasks", s.instanceTasks)
	s.mux.HandleFunc("GET /v1/instances/{id}/completion", s.completion)
	s.swaggerRoutes()
}

func actor(r *http.Request) string {
	return r.Header.Get("X-Actor-Id")
}

func writeJSON(w http.ResponseWriter, code int, v any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(code)
	_ = json.NewEncoder(w).Encode(v)
}

func writeErr(w http.ResponseWriter, err error) {
	code := http.StatusInternalServerError
	switch {
	case errors.Is(err, domain.ErrNotFound):
		code = http.StatusNotFound
	case errors.Is(err, domain.ErrForbidden), errors.Is(err, domain.ErrForbiddenTenant):
		code = http.StatusForbidden
	case errors.Is(err, domain.ErrUnauthorized):
		code = http.StatusUnauthorized
	case errors.Is(err, domain.ErrInvalid), errors.Is(err, domain.ErrNotOpen), errors.Is(err, domain.ErrEmptyGroup),
		errors.Is(err, domain.ErrNotClaimed):
		code = http.StatusBadRequest
	case errors.Is(err, domain.ErrConflict), errors.Is(err, domain.ErrAlreadyClaimed):
		code = http.StatusConflict
	}
	writeJSON(w, code, map[string]string{"error": err.Error()})
}

func (s *Server) health(w http.ResponseWriter, _ *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{"status": "ok"})
}

type registerReq struct {
	Key  string `json:"key"`
	Name string `json:"name"`
}

func (s *Server) register(w http.ResponseWriter, r *http.Request) {
	var req registerReq
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	out, err := s.engine.Register(r.Context(), req.Key, req.Name)
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusCreated, out)
}

func (s *Server) getDefinition(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.LatestDefinition(r.Context(), r.PathValue("key"))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

type startReq struct {
	ProcessKey string        `json:"processKey"`
	Initiator  string        `json:"initiator"`
	Parameters workflow.Vars `json:"parameters"`
}

func (s *Server) start(w http.ResponseWriter, r *http.Request) {
	var req startReq
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	if req.Initiator == "" {
		req.Initiator = actor(r)
	}
	out, err := s.engine.Start(r.Context(), req.ProcessKey, req.Initiator, req.Parameters)
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusCreated, out)
}

func (s *Server) listByProcessKey(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.ListByProcessKey(r.Context(), r.PathValue("processKey"))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

type toReq struct {
	Kind string   `json:"kind"`
	ID   string   `json:"id"`
	IDs  []string `json:"ids"`
}

type referReq struct {
	DefinitionKey    string        `json:"definitionKey"`
	ParentInstanceID string        `json:"parentInstanceId"`
	From             string        `json:"from"`
	Title            string        `json:"title"`
	Parameters       workflow.Vars `json:"parameters"`
	To               toReq         `json:"to"`
}

func (s *Server) refer(w http.ResponseWriter, r *http.Request) {
	var req referReq
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
		return
	}
	from := req.From
	if from == "" {
		from = actor(r)
	}
	out, err := s.engine.Refer(r.Context(), from, workflow.ReferInput{
		DefinitionKey:    req.DefinitionKey,
		ParentInstanceID: req.ParentInstanceID,
		Title:            req.Title,
		Parameters:       req.Parameters,
		ToKind:           workflow.AssigneeKind(req.To.Kind),
		ToID:             req.To.ID,
		ToIDs:            req.To.IDs,
	})
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusCreated, out)
}

func (s *Server) pendingTasks(w http.ResponseWriter, r *http.Request) {
	user := r.URL.Query().Get("user")
	group := r.URL.Query().Get("group")
	if user == "" && group == "" {
		user = actor(r)
	}
	out, err := s.engine.PendingTasks(r.Context(), user, group)
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

func (s *Server) getTask(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.GetTask(r.Context(), r.PathValue("id"))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

type actorReq struct {
	From string `json:"from"`
}

func (s *Server) decodeActor(r *http.Request) string {
	from := actor(r)
	var req actorReq
	if r.Body != nil && r.ContentLength != 0 {
		_ = json.NewDecoder(r.Body).Decode(&req)
		if req.From != "" {
			from = req.From
		}
	}
	return from
}

func (s *Server) claim(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.ClaimTask(r.Context(), r.PathValue("id"), s.decodeActor(r))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

func (s *Server) unclaim(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.UnclaimTask(r.Context(), r.PathValue("id"), s.decodeActor(r))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

type completeReq struct {
	From       string        `json:"from"`
	Note       string        `json:"note"`
	Parameters workflow.Vars `json:"parameters"`
}

func (s *Server) complete(w http.ResponseWriter, r *http.Request) {
	var req completeReq
	if r.Body != nil && r.ContentLength != 0 {
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{"error": err.Error()})
			return
		}
	}
	who := req.From
	if who == "" {
		who = actor(r)
	}
	out, err := s.engine.CompleteTask(r.Context(), r.PathValue("id"), who, req.Note, req.Parameters)
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

func (s *Server) getInstance(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.GetInstance(r.Context(), r.PathValue("id"))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

func (s *Server) instanceTasks(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.ListTasksByInstance(r.Context(), r.PathValue("id"))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}

func (s *Server) completion(w http.ResponseWriter, r *http.Request) {
	out, err := s.engine.Completion(r.Context(), r.PathValue("id"))
	if err != nil {
		writeErr(w, err)
		return
	}
	writeJSON(w, http.StatusOK, out)
}
