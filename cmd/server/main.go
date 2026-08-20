package main

import (
	"context"
	"log"
	"net/http"
	"os"
	"strings"
	"time"

	httpapi "github.com/mortenaho/workflowengine/internal/api/http"
	"github.com/mortenaho/workflowengine/internal/store/postgres"
	"github.com/mortenaho/workflowengine/pkg/workflow"
)

func main() {
	ctx := context.Background()
	dir := workflow.NewStaticDirectory(
		splitCSV(env("WF_USERS", "alice,bob,cara,dan,manager,ceo")),
		map[string][]string{
			"legal":   splitCSV(env("WF_GROUP_LEGAL", "bob,cara")),
			"finance": splitCSV(env("WF_GROUP_FINANCE", "dan,cara")),
		},
	)

	var store workflow.Store = workflow.NewMemoryStore()
	if dsn := os.Getenv("DATABASE_URL"); dsn != "" {
		pg, err := openPostgres(ctx, dsn)
		if err != nil {
			log.Fatalf("postgres: %v", err)
		}
		defer pg.Close()
		store = pg
		log.Print("store: postgres")
	} else {
		log.Print("store: memory")
	}

	eng := workflow.NewEngine(store, dir)
	keys := splitCSV(os.Getenv("WF_API_KEYS"))
	srv := httpapi.New(eng, keys...)

	addr := env("ADDR", ":8081")
	log.Printf("workflow engine listening on %s", addr)
	log.Printf("swagger UI: http://localhost%s/swagger", addr)
	if err := http.ListenAndServe(addr, srv.Handler()); err != nil {
		log.Fatal(err)
	}
}

func env(key, fallback string) string {
	if v := os.Getenv(key); v != "" {
		return v
	}
	return fallback
}

func splitCSV(s string) []string {
	parts := strings.Split(s, ",")
	out := make([]string, 0, len(parts))
	for _, p := range parts {
		p = strings.TrimSpace(p)
		if p != "" {
			out = append(out, p)
		}
	}
	return out
}

func openPostgres(ctx context.Context, dsn string) (*postgres.Store, error) {
	var last error
	for i := 0; i < 30; i++ {
		pg, err := postgres.Open(ctx, dsn)
		if err == nil {
			return pg, nil
		}
		last = err
		log.Printf("waiting for postgres: %v", err)
		time.Sleep(time.Second)
	}
	return nil, last
}
