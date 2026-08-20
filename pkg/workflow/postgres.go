package workflow

import (
	"context"

	"github.com/mortenaho/workflowengine/internal/store/postgres"
)

func OpenPostgres(ctx context.Context, dsn string) (Store, error) {
	return postgres.Open(ctx, dsn)
}
