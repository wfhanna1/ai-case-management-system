import { useParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
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
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import CircularProgress from '@mui/material/CircularProgress';
import Grid from '@mui/material/Grid2';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import { getCase } from '@/services/caseService';
import type { DocumentDto } from '@/services/documentService';
import { formatDate, STATUS_COLORS } from '@/utils/formatting';

function CaseDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['case', id],
    queryFn: () => getCase(id!),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError) {
    return (
      <Box sx={{ mt: 4 }}>
        <Alert severity="error" data-testid="case-error">
          {error instanceof Error ? error.message : 'Failed to load case'}
        </Alert>
      </Box>
    );
  }

  if (!data) return null;

  return (
    <Box sx={{ mt: 4 }}>
      <Button
        startIcon={<ArrowBackIcon />}
        onClick={() => navigate('/cases')}
        sx={{ mb: 2 }}
        data-testid="back-btn"
      >
        Back to Cases
      </Button>

      <Typography variant="h5" component="h1" gutterBottom fontWeight={700} data-testid="case-subject">
        Case: {data.subjectName}
      </Typography>

      <Paper elevation={2} sx={{ p: 3, mb: 3 }}>
        <Grid container spacing={2}>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="body2" color="text.secondary">Created</Typography>
            <Typography data-testid="case-created">{formatDate(data.createdAt)}</Typography>
          </Grid>
          <Grid size={{ xs: 12, sm: 6 }}>
            <Typography variant="body2" color="text.secondary">Last Updated</Typography>
            <Typography data-testid="case-updated">{formatDate(data.updatedAt)}</Typography>
          </Grid>
        </Grid>
      </Paper>

      <Typography variant="h6" gutterBottom fontWeight={600}>
        Documents ({data.documents.length})
      </Typography>

      <Paper elevation={2}>
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>File Name</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Submitted At</TableCell>
                <TableCell>Processed At</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.documents.length > 0 ? (
                data.documents.map((doc: DocumentDto) => (
                  <TableRow
                    key={doc.id}
                    hover
                    sx={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/documents/${doc.id}`)}
                    data-testid={`case-doc-${doc.id}`}
                  >
                    <TableCell>{doc.originalFileName}</TableCell>
                    <TableCell>
                      <Chip
                        label={doc.status}
                        color={STATUS_COLORS[doc.status] || 'default'}
                        size="small"
                      />
                    </TableCell>
                    <TableCell>{formatDate(doc.submittedAt)}</TableCell>
                    <TableCell>{formatDate(doc.processedAt)}</TableCell>
                  </TableRow>
                ))
              ) : (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 4 }}>
                    <Typography color="text.secondary" data-testid="no-case-docs">
                      No documents in this case
                    </Typography>
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

export default CaseDetailPage;
