import { useQuery } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Chip from '@mui/material/Chip';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import Grid from '@mui/material/Grid2';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import ErrorIcon from '@mui/icons-material/Error';
import WarningIcon from '@mui/icons-material/Warning';
import api from '@/services/api';
import type { HealthCheckResponse } from '@/types';

function HealthStatusIcon({ status }: { status: string }) {
  if (status === 'Healthy') return <CheckCircleIcon color="success" />;
  if (status === 'Degraded') return <WarningIcon color="warning" />;
  return <ErrorIcon color="error" />;
}

function HealthStatusChip({ status }: { status: string }) {
  const color =
    status === 'Healthy' ? 'success' : status === 'Degraded' ? 'warning' : 'error';
  return <Chip label={status} color={color} size="small" />;
}

function HomePage() {
  const {
    data: health,
    isLoading,
    isError,
    error,
  } = useQuery<HealthCheckResponse>({
    queryKey: ['health'],
    queryFn: async () => {
      const response = await api.get<HealthCheckResponse>('/health');
      return response.data;
    },
    refetchInterval: 30000,
  });

  return (
    <Box>
      <Box sx={{ mb: 4 }}>
        <Typography variant="h3" component="h1" gutterBottom fontWeight={700}>
          Intake Document Processor
        </Typography>
        <Typography variant="subtitle1" color="text.secondary">
          AI-powered case management system for automated document intake and processing.
        </Typography>
      </Box>

      <Paper sx={{ p: 3, mb: 3 }} elevation={1}>
        <Typography variant="h6" gutterBottom fontWeight={600}>
          System Health
        </Typography>

        {isLoading && (
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <CircularProgress size={20} />
            <Typography variant="body2" color="text.secondary">
              Checking system health...
            </Typography>
          </Box>
        )}

        {isError && (
          <Alert severity="error">
            Unable to reach the API.{' '}
            {error instanceof Error ? error.message : 'Connection refused.'}
          </Alert>
        )}

        {health && (
          <Box>
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 2 }}>
              <HealthStatusIcon status={health.status} />
              <Typography variant="body1" fontWeight={600}>
                Overall Status:
              </Typography>
              <HealthStatusChip status={health.status} />
              <Typography variant="body2" color="text.secondary" sx={{ ml: 1 }}>
                Response time: {health.totalDuration}
              </Typography>
            </Box>

            {health.entries && Object.keys(health.entries).length > 0 && (
              <Grid container spacing={2}>
                {Object.entries(health.entries).map(([name, entry]) => (
                  <Grid key={name} size={{ xs: 12, sm: 6, md: 4 }}>
                    <Paper variant="outlined" sx={{ p: 2 }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 1 }}>
                        <HealthStatusIcon status={entry.status} />
                        <Typography variant="body2" fontWeight={600}>
                          {name}
                        </Typography>
                      </Box>
                      <HealthStatusChip status={entry.status} />
                      {entry.description && (
                        <Typography variant="caption" display="block" sx={{ mt: 1 }} color="text.secondary">
                          {entry.description}
                        </Typography>
                      )}
                    </Paper>
                  </Grid>
                ))}
              </Grid>
            )}
          </Box>
        )}
      </Paper>
    </Box>
  );
}

export default HomePage;
