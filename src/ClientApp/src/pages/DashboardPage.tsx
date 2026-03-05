import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Grid from '@mui/material/Grid2';

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
      <Typography variant="h4" fontWeight={700} gutterBottom>
        {value}
      </Typography>
      <Typography variant="body2" color="text.secondary">
        {description}
      </Typography>
    </Paper>
  );
}

function DashboardPage() {
  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom fontWeight={700}>
        Dashboard
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Overview of intake document processing activity.
      </Typography>

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Total Cases" value="--" description="All time" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Pending Review" value="--" description="Awaiting action" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Processed Today" value="--" description="Last 24 hours" />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard title="Avg. Processing Time" value="--" description="Per document" />
        </Grid>
      </Grid>

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
