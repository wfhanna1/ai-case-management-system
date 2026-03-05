import { useState, type FormEvent } from 'react';
import { useNavigate, Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Alert from '@mui/material/Alert';
import MenuItem from '@mui/material/MenuItem';
import CircularProgress from '@mui/material/CircularProgress';
import Link from '@mui/material/Link';
import useAuthStore from '@/stores/authStore';
import { register, DEMO_TENANTS } from '@/services/authService';
import { parseJwt } from '@/utils/jwt';
import { validateEmail, validatePassword } from '@/utils/validation';

function RegisterPage() {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const [tenantId, setTenantId] = useState(DEMO_TENANTS[0].id);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const setFieldError = (field: string, err: string | null) => {
    setFieldErrors(prev => {
      const next = { ...prev };
      if (err) next[field] = err;
      else delete next[field];
      return next;
    });
  };

  const validateField = (field: string, value: string): string | null => {
    let err: string | null = null;
    if (field === 'email') err = validateEmail(value);
    if (field === 'password') {
      err = validatePassword(value);
      if (confirmPassword && value !== confirmPassword) {
        setFieldError('confirmPassword', 'Passwords do not match.');
      } else if (confirmPassword) {
        setFieldError('confirmPassword', null);
      }
    }
    if (field === 'confirmPassword') {
      err = value !== password ? 'Passwords do not match.' : null;
    }
    setFieldError(field, err);
    return err;
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    const emailErr = validateField('email', email);
    const passErr = validateField('password', password);
    const confirmErr = validateField('confirmPassword', confirmPassword);
    if (emailErr || passErr || confirmErr) return;

    setLoading(true);

    try {
      const res = await register({ tenantId, email, password, roles: ['IntakeWorker'] });
      if (res.error || !res.data) {
        setError(res.error?.message ?? 'Registration failed');
        return;
      }
      const claims = parseJwt(res.data.accessToken);
      setAuth(
        {
          id: res.data.userId,
          email: (claims.email as string) ?? email,
          roles: Array.isArray(claims.role) ? (claims.role as string[]) : [claims.role as string],
          tenantId,
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
            Create Account
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
              select
              label="Tenant"
              value={tenantId}
              onChange={e => setTenantId(e.target.value)}
              fullWidth
              margin="normal"
            >
              {DEMO_TENANTS.map(t => (
                <MenuItem key={t.id} value={t.id}>
                  {t.name}
                </MenuItem>
              ))}
            </TextField>
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
            />
            <TextField
              label="Password"
              type="password"
              fullWidth
              margin="normal"
              autoComplete="new-password"
              value={password}
              onChange={e => {
                setPassword(e.target.value);
                validateField('password', e.target.value);
              }}
              onBlur={() => validateField('password', password)}
              error={!!fieldErrors.password}
              helperText={fieldErrors.password}
            />
            <TextField
              label="Confirm Password"
              type="password"
              fullWidth
              margin="normal"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)}
              onBlur={() => validateField('confirmPassword', confirmPassword)}
              error={!!fieldErrors.confirmPassword}
              helperText={fieldErrors.confirmPassword}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              sx={{ mt: 3, mb: 2 }}
              size="large"
              disabled={loading}
            >
              {loading ? <CircularProgress size={24} color="inherit" /> : 'Register'}
            </Button>
          </Box>
          <Typography variant="body2" align="center">
            Already have an account?{' '}
            <Link component={RouterLink} to="/login">
              Sign In
            </Link>
          </Typography>
        </Paper>
      </Box>
    </Container>
  );
}

export default RegisterPage;
