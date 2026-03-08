const MS_ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function extractRoles(claims: Record<string, unknown>): string[] {
  const roleClaim = claims.role ?? claims[MS_ROLE_CLAIM];
  if (!roleClaim) return [];
  if (Array.isArray(roleClaim)) return roleClaim as string[];
  return [roleClaim as string];
}
