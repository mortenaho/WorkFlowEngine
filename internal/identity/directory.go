package identity

import "context"

type Directory interface {
	UserExists(ctx context.Context, userID string) (bool, error)
	GroupMembers(ctx context.Context, groupID string) ([]string, error)
	UserGroups(ctx context.Context, userID string) ([]string, error)
}

func IsMember(ctx context.Context, d Directory, userID, groupID string) (bool, error) {
	members, err := d.GroupMembers(ctx, groupID)
	if err != nil {
		return false, err
	}
	for _, m := range members {
		if m == userID {
			return true, nil
		}
	}
	return false, nil
}

type StaticDirectory struct {
	Users  map[string]struct{}
	Groups map[string][]string
}

func NewStaticDirectory(users []string, groups map[string][]string) *StaticDirectory {
	u := make(map[string]struct{}, len(users))
	for _, id := range users {
		u[id] = struct{}{}
	}
	g := make(map[string][]string, len(groups))
	for k, v := range groups {
		cp := make([]string, len(v))
		copy(cp, v)
		g[k] = cp
	}
	return &StaticDirectory{Users: u, Groups: g}
}

func (d *StaticDirectory) UserExists(_ context.Context, userID string) (bool, error) {
	_, ok := d.Users[userID]
	return ok, nil
}

func (d *StaticDirectory) GroupMembers(_ context.Context, groupID string) ([]string, error) {
	members, ok := d.Groups[groupID]
	if !ok {
		return nil, nil
	}
	out := make([]string, len(members))
	copy(out, members)
	return out, nil
}

func (d *StaticDirectory) UserGroups(_ context.Context, userID string) ([]string, error) {
	var out []string
	for gid, members := range d.Groups {
		for _, m := range members {
			if m == userID {
				out = append(out, gid)
				break
			}
		}
	}
	return out, nil
}
