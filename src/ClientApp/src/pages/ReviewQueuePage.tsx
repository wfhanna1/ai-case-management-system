import { useQuery } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import Chip from '@mui/material/Chip';
import Badge from '@mui/material/Badge';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import RateReviewIcon from '@mui/icons-material/RateReview';
import { getPendingReviews, type ReviewDocumentDto } from '@/services/reviewService';

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleString();
}

const STATUS_COLORS: Record<string, 'info' | 'warning' | 'success' | 'error' | 'default'> = {
  PendingReview: 'warning',
  InReview: 'info',
  Finalized: 'success',
};

function ReviewQueuePage() {
  const navigate = useNavigate();
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['pendingReviews'],
    queryFn: getPendingReviews,
    refetchInterval: 5000,
  });

  const pendingCount = data?.length ?? 0;

  return (
    <Box sx={{ mt: 4 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 2 }}>
        <Typography variant="h5" component="h1" fontWeight={700}>
          Review Queue
        </Typography>
        <Badge badgeContent={pendingCount} color="warning" data-testid="pending-badge">
          <RateReviewIcon color="action" />
        </Badge>
      </Box>

      {isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error instanceof Error ? error.message : 'Failed to load pending reviews'}
        </Alert>
      )}

      <Paper elevation={2}>
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>File Name</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Submitted At</TableCell>
                <TableCell>Processed At</TableCell>
                <TableCell align="right">Action</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4 }}>
                    <CircularProgress />
                  </TableCell>
                </TableRow>
              ) : data && data.length > 0 ? (
                data.map((doc: ReviewDocumentDto) => (
                  <TableRow key={doc.id} hover>
                    <TableCell>{doc.originalFileName}</TableCell>
                    <TableCell>
                      <Chip
                        label={doc.status}
                        color={STATUS_COLORS[doc.status] ?? 'default'}
                        size="small"
                      />
                    </TableCell>
                    <TableCell>{formatDate(doc.submittedAt)}</TableCell>
                    <TableCell>{formatDate(doc.processedAt)}</TableCell>
                    <TableCell align="right">
                      <Button
                        variant="contained"
                        size="small"
                        onClick={() => navigate(`/reviews/${doc.id}`)}
                        data-testid={`review-btn-${doc.id}`}
                      >
                        Review
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              ) : (
                <TableRow>
                  <TableCell colSpan={5} align="center" sx={{ py: 4 }}>
                    <Typography color="text.secondary">No documents pending review</Typography>
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </Paper>
    </Box>
  );
}

export default ReviewQueuePage;
