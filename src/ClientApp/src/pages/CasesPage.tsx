import { useState } from 'react';
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
import TablePagination from '@mui/material/TablePagination';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Chip from '@mui/material/Chip';
import { getCases } from '@/services/caseService';
import { formatDate } from '@/utils/formatting';

function CasesPage() {
  const navigate = useNavigate();
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['cases', page + 1, pageSize],
    queryFn: () => getCases(page + 1, pageSize),
  });

  return (
    <Box sx={{ mt: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom fontWeight={700}>
        Cases
      </Typography>

      {isError && (
        <Alert severity="error" sx={{ mb: 2 }} data-testid="cases-error">
          {error instanceof Error ? error.message : 'Failed to load cases'}
        </Alert>
      )}

      <Paper elevation={2}>
        <TableContainer>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Subject Name</TableCell>
                <TableCell>Documents</TableCell>
                <TableCell>Created</TableCell>
                <TableCell>Last Updated</TableCell>
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
                data.items.map(c => (
                  <TableRow
                    key={c.id}
                    hover
                    sx={{ cursor: 'pointer' }}
                    onClick={() => navigate(`/cases/${c.id}`)}
                    data-testid={`case-row-${c.id}`}
                  >
                    <TableCell>{c.subjectName}</TableCell>
                    <TableCell>
                      <Chip label={c.documentCount} size="small" color="primary" />
                    </TableCell>
                    <TableCell>{formatDate(c.createdAt)}</TableCell>
                    <TableCell>{formatDate(c.updatedAt)}</TableCell>
                  </TableRow>
                ))
              ) : (
                <TableRow>
                  <TableCell colSpan={4} align="center" sx={{ py: 4 }}>
                    <Typography color="text.secondary" data-testid="no-cases">
                      No cases found
                    </Typography>
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
            data-testid="cases-pagination"
          />
        )}
      </Paper>
    </Box>
  );
}

export default CasesPage;
