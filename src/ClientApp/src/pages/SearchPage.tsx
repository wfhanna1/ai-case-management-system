import { useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Select from '@mui/material/Select';
import MenuItem from '@mui/material/MenuItem';
import FormControl from '@mui/material/FormControl';
import InputLabel from '@mui/material/InputLabel';
import Button from '@mui/material/Button';
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
import Grid from '@mui/material/Grid2';
import SearchIcon from '@mui/icons-material/Search';
import { useDocumentSearch } from '@/hooks/useDocumentSearch';
import { formatDate, STATUS_COLORS } from '@/utils/formatting';
import type { DocumentDto } from '@/services/documentService';

const STATUS_OPTIONS = [
  '', 'Submitted', 'Processing', 'Completed', 'Failed', 'PendingReview', 'InReview', 'Finalized',
];

function SearchPage() {
  const navigate = useNavigate();
  const today = new Date().toISOString().split('T')[0];

  const {
    fileName, setFileName,
    status, setStatus,
    fieldValue, setFieldValue,
    fromDate, setFromDate,
    toDate, setToDate,
    page, setPage,
    pageSize, setPageSize,
    searchTriggered,
    handleSearch,
    handleClear,
    dateError,
    data,
    isLoading,
    isError,
    error,
  } = useDocumentSearch();

  const onSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!dateError) {
      handleSearch();
    }
  };

  return (
    <Box sx={{ mt: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom fontWeight={700}>
        Search Documents
      </Typography>

      <Paper elevation={2} sx={{ p: 3, mb: 3 }}>
        <form onSubmit={onSubmit}>
          <Grid container spacing={2} alignItems="center">
            <Grid size={{ xs: 12, sm: 6, md: 3 }}>
              <TextField
                fullWidth
                label="File Name"
                value={fileName}
                onChange={e => setFileName(e.target.value)}
                data-testid="search-filename"
                size="small"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 2 }}>
              <FormControl fullWidth size="small">
                <InputLabel>Status</InputLabel>
                <Select
                  value={status}
                  label="Status"
                  onChange={e => setStatus(e.target.value)}
                  data-testid="search-status"
                >
                  <MenuItem value="">All</MenuItem>
                  {STATUS_OPTIONS.filter(Boolean).map(s => (
                    <MenuItem key={s} value={s}>{s}</MenuItem>
                  ))}
                </Select>
              </FormControl>
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 2 }}>
              <TextField
                fullWidth
                label="Field Value"
                value={fieldValue}
                onChange={e => setFieldValue(e.target.value)}
                data-testid="search-fieldvalue"
                size="small"
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 2 }}>
              <TextField
                fullWidth
                type="date"
                label="From"
                value={fromDate}
                onChange={e => setFromDate(e.target.value)}
                data-testid="search-from"
                size="small"
                slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: today } }}
              />
            </Grid>
            <Grid size={{ xs: 12, sm: 6, md: 2 }}>
              <TextField
                fullWidth
                type="date"
                label="To"
                value={toDate}
                onChange={e => setToDate(e.target.value)}
                data-testid="search-to"
                size="small"
                error={!!dateError}
                helperText={dateError}
                slotProps={{ inputLabel: { shrink: true }, htmlInput: { max: today } }}
              />
            </Grid>
            <Grid size={{ xs: 12, md: 'auto' }}>
              <Box sx={{ display: 'flex', gap: 1 }}>
                <Button
                  type="submit"
                  variant="contained"
                  data-testid="search-btn"
                  startIcon={<SearchIcon />}
                  disabled={!!dateError || isLoading}
                >
                  Search
                </Button>
                <Button variant="outlined" onClick={handleClear} data-testid="clear-btn">
                  Clear
                </Button>
              </Box>
            </Grid>
          </Grid>
        </form>
      </Paper>

      {isError && (
        <Alert severity="error" sx={{ mb: 2 }} data-testid="search-error">
          {error instanceof Error ? error.message : 'Search failed'}
        </Alert>
      )}

      {!searchTriggered ? (
        <Paper elevation={2} sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary" data-testid="search-prompt">
            Use the filters above and click Search
          </Typography>
        </Paper>
      ) : (
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
                  data.items.map((doc: DocumentDto) => (
                    <TableRow
                      key={doc.id}
                      hover
                      sx={{ cursor: 'pointer' }}
                      onClick={() => navigate(`/documents/${doc.id}`)}
                      data-testid={`search-result-${doc.id}`}
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
                      <Typography color="text.secondary" data-testid="no-results">
                        No documents found
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
              }}
              data-testid="search-pagination"
            />
          )}
        </Paper>
      )}
    </Box>
  );
}

export default SearchPage;
