package httpapi

import (
	_ "embed"
	"net/http"
)

//go:embed openapi.yaml
var openAPIYAML []byte

//go:embed swagger.html
var swaggerHTML []byte

func (s *Server) swaggerRoutes() {
	s.mux.HandleFunc("GET /", s.redirectSwagger)
	s.mux.HandleFunc("GET /openapi.yaml", s.serveOpenAPI)
	s.mux.HandleFunc("GET /swagger", s.serveSwaggerUI)
	s.mux.HandleFunc("GET /swagger/", s.serveSwaggerUI)
	s.mux.HandleFunc("GET /docs", s.redirectSwagger)
}

func (s *Server) serveOpenAPI(w http.ResponseWriter, _ *http.Request) {
	w.Header().Set("Content-Type", "application/yaml; charset=utf-8")
	w.Header().Set("Cache-Control", "no-cache")
	_, _ = w.Write(openAPIYAML)
}

func (s *Server) serveSwaggerUI(w http.ResponseWriter, r *http.Request) {
	switch r.URL.Path {
	case "/swagger", "/swagger/":
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		_, _ = w.Write(swaggerHTML)
	default:
		http.NotFound(w, r)
	}
}

func (s *Server) redirectSwagger(w http.ResponseWriter, r *http.Request) {
	http.Redirect(w, r, "/swagger", http.StatusFound)
}
