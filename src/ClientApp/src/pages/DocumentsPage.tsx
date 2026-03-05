import { useState } from 'react';
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
import TablePagination from '@mui/material/TablePagination';
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
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(10);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['documents', page + 1, pageSize],
    queryFn: () => getDocuments(page + 1, pageSize),
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
              ) : data && data.items.length > 0 ? (
                data.items.map(doc => (
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
        {data && data.totalCount > 0 && (
          <TablePagination
            component="div"
            count={data.totalCount}
            page={page}
            onPageChange={(_, newPage) => setPage(newPage)}
            rowsPerPage={pageSize}
            onRowsPerPageChange={e => {
              setPageSize(parseInt(e.target.value, 10));
              setPage(0);
            }}
            rowsPerPageOptions={[5, 10, 25]}
          />
        )}
      </Paper>
    </Box>
  );
}

export default DocumentsPage;
