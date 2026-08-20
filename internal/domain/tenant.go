package domain

import "context"

const DefaultTenant = "default"

type tenantCtxKey struct{}

func WithTenant(ctx context.Context, tenantID string) context.Context {
	if tenantID == "" {
		tenantID = DefaultTenant
	}
	return context.WithValue(ctx, tenantCtxKey{}, tenantID)
}

func TenantID(ctx context.Context) string {
	if ctx == nil {
		return DefaultTenant
	}
	if v, ok := ctx.Value(tenantCtxKey{}).(string); ok && v != "" {
		return v
	}
	return DefaultTenant
}

func NormalizeTenant(id string) string {
	if id == "" {
		return DefaultTenant
	}
	return id
}
