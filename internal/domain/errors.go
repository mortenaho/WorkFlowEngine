package domain

import "errors"

var (
	ErrNotFound        = errors.New("not found")
	ErrConflict        = errors.New("conflict")
	ErrForbidden       = errors.New("forbidden")
	ErrInvalid         = errors.New("invalid")
	ErrNotOpen         = errors.New("task is not open")
	ErrAlreadyClaimed  = errors.New("task already claimed")
	ErrNotClaimed      = errors.New("task is not claimed")
	ErrEmptyGroup      = errors.New("group has no members")
	ErrUnauthorized    = errors.New("unauthorized")
	ErrForbiddenTenant = errors.New("tenant mismatch")
)
