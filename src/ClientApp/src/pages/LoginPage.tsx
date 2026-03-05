import { useState, type FormEvent } from 'react';
import { useNavigate, Navigate, Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Link from '@mui/material/Link';
import useAuthStore from '@/stores/authStore';
import { login } from '@/services/authService';
import { parseJwt } from '@/utils/jwt';
import { validateEmail, validateRequired } from '@/utils/validation';

function LoginPage() {
  const navigate = useNavigate();
  const { setAuth, isAuthenticated } = useAuthStore();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }

  const validateField = (field: string, value: string) => {
    let err: string | null = null;
    if (field === 'email') err = validateEmail(value);
    if (field === 'password') err = validateRequired(value, 'Password');
    setFieldErrors(prev => {
      const next = { ...prev };
      if (err) next[field] = err;
      else delete next[field];
      return next;
    });
    return err;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    const emailErr = validateField('email', email);
    const passErr = validateField('password', password);
    if (emailErr || passErr) return;

    setLoading(true);

    try {
      const res = await login({ email, password });
      if (res.error || !res.data) {
        setError(res.error?.message ?? 'Login failed');
        return;
      }
      const claims = parseJwt(res.data.accessToken);
      const roleClaim = claims.role
        ?? claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
      const roles = Array.isArray(roleClaim)
        ? (roleClaim as string[])
        : roleClaim ? [roleClaim as string] : [];
      setAuth(
        {
          id: res.data.userId,
          email: (claims.email as string) ?? email,
          roles,
          tenantId: (claims.tenant_id as string) ?? '',
        },
        res.data.accessToken,
        res.data.refreshToken
      );
      navigate('/dashboard', { replace: true });
    } catch {
      setError('Unable to connect to the server.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Container maxWidth="sm">
      <Box
        sx={{
          minHeight: '80vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Paper sx={{ p: 4, width: '100%' }} elevation={2}>
          <Typography variant="h5" component="h1" gutterBottom fontWeight={700} align="center">
            Sign In
          </Typography>
          <Typography variant="body2" color="text.secondary" align="center" sx={{ mb: 3 }}>
            Intake Document Processor
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Box component="form" noValidate onSubmit={handleSubmit}>
            <TextField
              label="Email"
              type="email"
              fullWidth
              margin="normal"
              autoComplete="email"
              autoFocus
              value={email}
              onChange={e => setEmail(e.target.value)}
              onBlur={() => validateField('email', email)}
              error={!!fieldErrors.email}
              helperText={fieldErrors.email}
              inputProps={{ 'aria-label': 'email address' }}
            />
            <TextField
              label="Password"
              type="password"
              fullWidth
              margin="normal"
              autoComplete="current-password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              onBlur={() => validateField('password', password)}
              error={!!fieldErrors.password}
              helperText={fieldErrors.password}
              inputProps={{ 'aria-label': 'password' }}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              sx={{ mt: 3, mb: 2 }}
              size="large"
              disabled={loading}
            >
              {loading ? <CircularProgress size={24} color="inherit" /> : 'Sign In'}
            </Button>
          </Box>
          <Typography variant="body2" align="center">
            Don&apos;t have an account?{' '}
            <Link component={RouterLink} to="/register">
              Register
            </Link>
          </Typography>
        </Paper>
      </Box>
    </Container>
  );
}

export default LoginPage;
