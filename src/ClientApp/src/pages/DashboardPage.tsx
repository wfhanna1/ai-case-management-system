import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Grid from '@mui/material/Grid2';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { useQuery } from '@tanstack/react-query';
import { getDashboardStats } from '../services/documentService';

interface StatCardProps {
  title: string;
  value: string;
  description: string;
}

function StatCard({ title, value, description }: StatCardProps) {
  return (
    <Paper sx={{ p: 3 }} elevation={1}>
      <Typography variant="body2" color="text.secondary" gutterBottom>
        {title}
      </Typography>
      <Typography variant="h4" fontWeight={700} gutterBottom data-testid={`stat-${title.toLowerCase().replace(/[^a-z]+/g, '-')}`}>
        {value}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {description}
      </Typography>
    </Paper>
  );
}

function DashboardPage() {
  const { data: stats, isLoading, error } = useQuery({
    queryKey: ['dashboardStats'],
    queryFn: getDashboardStats,
    refetchInterval: 30_000,
  });

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom fontWeight={700}>
        Dashboard
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Overview of intake document processing activity.
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 3 }}>
          Failed to load dashboard stats.
        </Alert>
      )}

      {isLoading ? (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 6 }}>
          <CircularProgress />
        </Box>
      ) : (
        <Grid container spacing={3}>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <StatCard title="Total Cases" value={stats ? String(stats.totalCases) : '--'} description="All time" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <StatCard title="Pending Review" value={stats ? String(stats.pendingReview) : '--'} description="Awaiting action" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <StatCard title="Processed Today" value={stats ? String(stats.processedToday) : '--'} description="Since midnight UTC" />
          </Grid>
          <Grid size={{ xs: 12, sm: 6, md: 3 }}>
            <StatCard title="Avg. Processing Time" value={stats?.averageProcessingTime ?? '--'} description="Per document" />
          </Grid>
        </Grid>
      )}

      <Paper sx={{ p: 3, mt: 3 }} elevation={1}>
        <Typography variant="h6" gutterBottom fontWeight={600}>
          Recent Activity
        </Typography>
        <Typography variant="body2" color="text.secondary">
          Activity feed will appear here once cases are being processed.
        </Typography>
      </Paper>
    </Box>
  );
}

export default DashboardPage;
