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
import CircularProgress from '@mui/material/CircularProgress';
import { getDocuments, type DocumentDto } from '@/services/documentService';

const STATUS_COLORS: Record<DocumentDto['status'], 'info' | 'warning' | 'success' | 'error'> = {
  Submitted: 'info',
  Processing: 'warning',
  Completed: 'success',
  Failed: 'error',
};

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '-';
  return new Date(dateStr).toLocaleString();
}

function DocumentsPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['documents'],
    queryFn: () => getDocuments(),
    refetchInterval: 5000,
  });

  return (
    <Box sx={{ mt: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom fontWeight={700}>
        Documents
      </Typography>

      {isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error instanceof Error ? error.message : 'Failed to load documents'}
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
              </TableRow>
            </TableHead>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 4 }}>
                    <CircularProgress />
                  </TableCell>
                </TableRow>
              ) : data && data.length > 0 ? (
                data.map(doc => (
                  <TableRow key={doc.id}>
                    <TableCell>{doc.originalFileName}</TableCell>
                    <TableCell>
                      <Chip
                        label={doc.status}
                        color={STATUS_COLORS[doc.status]}
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
                    <Typography color="text.secondary">No documents found</Typography>
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

export default DocumentsPage;
