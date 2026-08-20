package httpapi

import (
	"net/http"
	"strings"

	"github.com/mortenaho/workflowengine/internal/domain"
)

func (s *Server) Handler() http.Handler {
	h := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		tenant := r.Header.Get("X-Tenant-Id")
		r = r.WithContext(domain.WithTenant(r.Context(), tenant))
		s.mux.ServeHTTP(w, r)
	})
	if len(s.apiKeys) == 0 {
		return h
	}
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if publicPath(r.URL.Path) {
			h.ServeHTTP(w, r)
			return
		}
		key := r.Header.Get("X-API-Key")
		if key == "" {
			if a := r.Header.Get("Authorization"); strings.HasPrefix(strings.ToLower(a), "bearer ") {
				key = strings.TrimSpace(a[7:])
			}
		}
		if _, ok := s.apiKeys[key]; !ok {
			writeJSON(w, http.StatusUnauthorized, map[string]string{"error": domain.ErrUnauthorized.Error()})
			return
		}
		h.ServeHTTP(w, r)
	})
}

func publicPath(p string) bool {
	switch p {
	case "/", "/health", "/openapi.yaml", "/swagger", "/swagger/", "/docs":
		return true
	default:
		return false
	}
}

func parseAPIKeys(keys []string) map[string]struct{} {
	out := make(map[string]struct{}, len(keys))
	for _, k := range keys {
		k = strings.TrimSpace(k)
		if k != "" {
			out[k] = struct{}{}
		}
	}
	return out
}
