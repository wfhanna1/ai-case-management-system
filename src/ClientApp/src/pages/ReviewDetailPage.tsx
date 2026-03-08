import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Grid from '@mui/material/Grid2';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Alert from '@mui/material/Alert';
import CircularProgress from '@mui/material/CircularProgress';
import Table from '@mui/material/Table';
import TableBody from '@mui/material/TableBody';
import TableCell from '@mui/material/TableCell';
import TableContainer from '@mui/material/TableContainer';
import TableHead from '@mui/material/TableHead';
import TableRow from '@mui/material/TableRow';
import TextField from '@mui/material/TextField';
import IconButton from '@mui/material/IconButton';
import Drawer from '@mui/material/Drawer';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogActions from '@mui/material/DialogActions';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import EditIcon from '@mui/icons-material/Edit';
import CheckIcon from '@mui/icons-material/Check';
import CloseIcon from '@mui/icons-material/Close';
import HistoryIcon from '@mui/icons-material/History';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import Accordion from '@mui/material/Accordion';
import AccordionSummary from '@mui/material/AccordionSummary';
import AccordionDetails from '@mui/material/AccordionDetails';
import ExpandMoreIcon from '@mui/icons-material/ExpandMore';
import CompareArrowsIcon from '@mui/icons-material/CompareArrows';
import {
  getReview,
  startReview,
  correctField,
  finalizeReview,
  getAuditTrail,
  getSimilarCases,
  getDocumentFileBlob,
  type ExtractedFieldDto,
  type AuditLogEntryDto,
  type SimilarCaseDto,
} from '@/services/reviewService';
import { formatDate, confidenceColor, confidenceLabel } from '@/utils/formatting';

function ReviewDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [editingField, setEditingField] = useState<string | null>(null);
  const [editValue, setEditValue] = useState('');
  const [auditOpen, setAuditOpen] = useState(false);
  const [finalizeOpen, setFinalizeOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const { data: doc, isLoading, isError, error } = useQuery({
    queryKey: ['review', id],
    queryFn: () => getReview(id!),
    enabled: !!id,
    staleTime: 0,
    refetchInterval: 3000,
  });

  const { data: auditData, isLoading: auditLoading } = useQuery({
    queryKey: ['audit', id],
    queryFn: () => getAuditTrail(id!),
    enabled: auditOpen && !!id,
    staleTime: 0,
  });

  const { data: similarData, isLoading: similarLoading } = useQuery({
    queryKey: ['similar-cases', id],
    queryFn: () => getSimilarCases(id!),
    enabled: !!id,
    staleTime: 0,
  });

  const { data: fileBlob, isError: fileError } = useQuery({
    queryKey: ['document-file', id],
    queryFn: () => getDocumentFileBlob(id!),
    enabled: !!id,
    staleTime: Infinity,
    retry: false,
  });

  const [previewUrl, setPreviewUrl] = useState<string | null>(null);

  useEffect(() => {
    if (!fileBlob) return;
    const url = URL.createObjectURL(fileBlob);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [fileBlob]);

  const startMutation = useMutation({
    mutationFn: () => startReview(id!),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['review', id] });
      setActionError(null);
    },
    onError: (err: Error) => setActionError(err.message),
  });

  const correctMutation = useMutation({
    mutationFn: ({ fieldName, newValue }: { fieldName: string; newValue: string }) =>
      correctField(id!, fieldName, newValue),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['review', id] });
      setEditingField(null);
      setEditValue('');
      setActionError(null);
    },
    onError: (err: Error) => setActionError(err.message),
  });

  const finalizeMutation = useMutation({
    mutationFn: () => finalizeReview(id!),
    onSuccess: async () => {
      queryClient.invalidateQueries({ queryKey: ['review', id] });
      await queryClient.invalidateQueries({ queryKey: ['pendingReviews'] });
      setFinalizeOpen(false);
      setActionError(null);
      navigate('/reviews');
    },
    onError: (err: Error) => {
      setFinalizeOpen(false);
      setActionError(err.message);
    },
  });

  const handleStartEdit = (field: ExtractedFieldDto) => {
    setEditingField(field.name);
    setEditValue(field.correctedValue ?? field.value);
  };

  const handleSaveEdit = (fieldName: string) => {
    correctMutation.mutate({ fieldName, newValue: editValue });
  };

  const handleCancelEdit = () => {
    setEditingField(null);
    setEditValue('');
  };

  if (isLoading) {
    return (
      <Box sx={{ mt: 4, display: 'flex', justifyContent: 'center' }}>
        <CircularProgress />
      </Box>
    );
  }

  if (isError || !doc) {
    return (
      <Box sx={{ mt: 4 }}>
        <Alert severity="error">
          {error instanceof Error ? error.message : 'Failed to load document'}
        </Alert>
      </Box>
    );
  }

  const isInReview = doc.status === 'InReview';
  const isFinalized = doc.status === 'Finalized';

  return (
    <Box sx={{ mt: 4 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', mb: 2, gap: 1 }}>
        <IconButton onClick={() => navigate('/reviews')} data-testid="back-btn">
          <ArrowBackIcon />
        </IconButton>
        <Typography variant="h5" component="h1" fontWeight={700} sx={{ flexGrow: 1 }}>
          {doc.originalFileName}
        </Typography>
        <Chip
          label={doc.status}
          color={
            doc.status === 'Finalized'
              ? 'success'
              : doc.status === 'InReview'
                ? 'info'
                : 'warning'
          }
          data-testid="review-status"
        />
        <Button
          variant="outlined"
          startIcon={<HistoryIcon />}
          onClick={() => setAuditOpen(true)}
          data-testid="audit-btn"
        >
          Audit History
        </Button>
      </Box>

      {actionError && (
        <Alert severity="error" sx={{ mb: 2 }} onClose={() => setActionError(null)}>
          {actionError}
        </Alert>
      )}

      {doc.status === 'PendingReview' && (
        <Alert severity="info" sx={{ mb: 2 }}>
          This document is pending review.{' '}
          <Button
            size="small"
            variant="contained"
            onClick={() => startMutation.mutate()}
            disabled={startMutation.isPending}
            data-testid="start-review-btn"
          >
            {startMutation.isPending ? 'Starting...' : 'Start Review'}
          </Button>
        </Alert>
      )}

      <Grid container spacing={3}>
        {/* Left side: Document info */}
        <Grid size={{ xs: 12, md: 5 }}>
          <Paper elevation={2} sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Document Info
            </Typography>
            <Table size="small">
              <TableBody>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>File</TableCell>
                  <TableCell>{doc.originalFileName}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>Status</TableCell>
                  <TableCell>{doc.status}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>Submitted</TableCell>
                  <TableCell>{formatDate(doc.submittedAt)}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>Processed</TableCell>
                  <TableCell>{formatDate(doc.processedAt)}</TableCell>
                </TableRow>
                <TableRow>
                  <TableCell sx={{ fontWeight: 600 }}>Reviewed</TableCell>
                  <TableCell>{formatDate(doc.reviewedAt)}</TableCell>
                </TableRow>
              </TableBody>
            </Table>
          </Paper>

          <Paper elevation={2} sx={{ p: 2, mt: 2 }} data-testid="document-preview">
            <Typography variant="h6" gutterBottom>
              Document Preview
            </Typography>
            {previewUrl ? (
              fileBlob?.type === 'application/pdf' ? (
                <Box
                  component="iframe"
                  src={previewUrl}
                  sx={{ width: '100%', height: 600, border: 'none' }}
                  title="Document preview"
                />
              ) : (
                <Box
                  component="img"
                  src={previewUrl}
                  alt={doc.originalFileName}
                  sx={{ width: '100%', maxHeight: 600, objectFit: 'contain' }}
                />
              )
            ) : fileError ? (
              <Alert severity="warning">
                Unable to load document preview. The file may not be available.
              </Alert>
            ) : (
              <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
                <CircularProgress size={24} />
              </Box>
            )}
          </Paper>
        </Grid>

        {/* Right side: Extracted fields */}
        <Grid size={{ xs: 12, md: 7 }}>
          <Paper elevation={2} sx={{ p: 3 }}>
            <Typography variant="h6" gutterBottom>
              Extracted Fields
            </Typography>
            <TableContainer>
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell>Field</TableCell>
                    <TableCell>Value</TableCell>
                    <TableCell>Confidence</TableCell>
                    <TableCell>Corrected</TableCell>
                    {isInReview && <TableCell align="right">Edit</TableCell>}
                  </TableRow>
                </TableHead>
                <TableBody>
                  {doc.extractedFields.length > 0 ? (
                    doc.extractedFields.map((field: ExtractedFieldDto) => (
                      <TableRow key={field.name} data-testid={`field-row-${field.name}`}>
                        <TableCell sx={{ fontWeight: 600 }}>{field.name}</TableCell>
                        <TableCell>{field.value}</TableCell>
                        <TableCell>
                          <Chip
                            label={confidenceLabel(field.confidence)}
                            color={confidenceColor(field.confidence)}
                            size="small"
                            data-testid={`confidence-${field.name}`}
                          />
                        </TableCell>
                        <TableCell>
                          {editingField === field.name ? (
                            <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                              <TextField
                                size="small"
                                value={editValue}
                                onChange={e => setEditValue(e.target.value)}
                                data-testid={`edit-input-${field.name}`}
                                autoFocus
                              />
                              <IconButton
                                size="small"
                                color="primary"
                                onClick={() => handleSaveEdit(field.name)}
                                disabled={correctMutation.isPending}
                                data-testid={`save-edit-${field.name}`}
                              >
                                <CheckIcon fontSize="small" />
                              </IconButton>
                              <IconButton
                                size="small"
                                onClick={handleCancelEdit}
                                data-testid={`cancel-edit-${field.name}`}
                              >
                                <CloseIcon fontSize="small" />
                              </IconButton>
                            </Box>
                          ) : (
                            field.correctedValue ?? '-'
                          )}
                        </TableCell>
                        {isInReview && editingField !== field.name && (
                          <TableCell align="right">
                            <IconButton
                              size="small"
                              onClick={() => handleStartEdit(field)}
                              data-testid={`edit-btn-${field.name}`}
                            >
                              <EditIcon fontSize="small" />
                            </IconButton>
                          </TableCell>
                        )}
                        {isInReview && editingField === field.name && (
                          <TableCell />
                        )}
                      </TableRow>
                    ))
                  ) : (
                    <TableRow>
                      <TableCell colSpan={isInReview ? 5 : 4} align="center">
                        <Typography color="text.secondary">No extracted fields</Typography>
                      </TableCell>
                    </TableRow>
                  )}
                </TableBody>
              </Table>
            </TableContainer>
          </Paper>
        </Grid>
      </Grid>

      {/* Similar Cases Panel */}
      <Accordion sx={{ mt: 3 }} data-testid="similar-cases-panel">
        <AccordionSummary expandIcon={<ExpandMoreIcon />}>
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
            <CompareArrowsIcon color="action" />
            <Typography variant="h6">
              Similar Cases
              {similarData && similarData.items.length > 0 && (
                <Chip
                  label={similarData.items.length}
                  size="small"
                  color="primary"
                  sx={{ ml: 1 }}
                />
              )}
            </Typography>
          </Box>
        </AccordionSummary>
        <AccordionDetails>
          {similarLoading ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 3 }}>
              <CircularProgress data-testid="similar-loading" />
            </Box>
          ) : similarData && similarData.items.length > 0 ? (
            <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              {similarData.items.map((item: SimilarCaseDto) => (
                <Paper
                  key={item.documentId}
                  variant="outlined"
                  sx={{ p: 2 }}
                  data-testid={`similar-case-${item.documentId}`}
                >
                  <Box sx={{ display: 'flex', alignItems: 'center', mb: 1, gap: 1 }}>
                    <Chip
                      label={`${(item.score * 100).toFixed(0)}% match`}
                      size="small"
                      color={item.score >= 0.9 ? 'success' : item.score >= 0.7 ? 'warning' : 'default'}
                      data-testid={`score-badge-${item.documentId}`}
                    />
                  </Box>
                  <Typography variant="body2" sx={{ mb: 1 }}>
                    {item.summary}
                  </Typography>
                  {Object.keys(item.metadata).length > 0 && (
                    <Accordion variant="outlined" disableGutters>
                      <AccordionSummary expandIcon={<ExpandMoreIcon />} sx={{ minHeight: 36 }}>
                        <Typography variant="caption" color="text.secondary">
                          Field Details
                        </Typography>
                      </AccordionSummary>
                      <AccordionDetails sx={{ pt: 0 }}>
                        <Table size="small">
                          <TableBody>
                            {Object.entries(item.metadata).map(([key, value]) => (
                              <TableRow key={key}>
                                <TableCell sx={{ fontWeight: 600, border: 'none', py: 0.5 }}>
                                  {key}
                                </TableCell>
                                <TableCell sx={{ border: 'none', py: 0.5 }}>
                                  {value}
                                </TableCell>
                              </TableRow>
                            ))}
                          </TableBody>
                        </Table>
                      </AccordionDetails>
                    </Accordion>
                  )}
                </Paper>
              ))}
            </Box>
          ) : (
            <Typography
              color="text.secondary"
              align="center"
              sx={{ py: 3 }}
              data-testid="no-similar-cases"
            >
              No similar cases found
            </Typography>
          )}
        </AccordionDetails>
      </Accordion>

      {isInReview && (
        <Box sx={{ mt: 3, display: 'flex', justifyContent: 'flex-end' }}>
          <Button
            variant="contained"
            color="success"
            size="large"
            onClick={() => setFinalizeOpen(true)}
            data-testid="finalize-btn"
          >
            Finalize Review
          </Button>
        </Box>
      )}

      {isFinalized && (
        <Alert severity="success" sx={{ mt: 3 }}>
          This document has been finalized.
        </Alert>
      )}

      {/* Finalize confirmation dialog */}
      <Dialog open={finalizeOpen} onClose={() => setFinalizeOpen(false)}>
        <DialogTitle>Finalize Review</DialogTitle>
        <DialogContent>
          <DialogContentText>
            Are you sure you want to finalize this review? This action cannot be undone.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setFinalizeOpen(false)}>Cancel</Button>
          <Button
            onClick={() => finalizeMutation.mutate()}
            color="success"
            variant="contained"
            disabled={finalizeMutation.isPending}
            data-testid="confirm-finalize-btn"
          >
            {finalizeMutation.isPending ? 'Finalizing...' : 'Confirm'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Audit history drawer */}
      <Drawer
        anchor="right"
        open={auditOpen}
        onClose={() => setAuditOpen(false)}
        PaperProps={{ sx: { width: 400 } }}
      >
        <Box sx={{ p: 2 }}>
          <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
            <Typography variant="h6" sx={{ flexGrow: 1 }}>
              Audit History
            </Typography>
            <IconButton onClick={() => setAuditOpen(false)}>
              <CloseIcon />
            </IconButton>
          </Box>
          {auditLoading ? (
            <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
              <CircularProgress />
            </Box>
          ) : auditData && auditData.length > 0 ? (
            <List data-testid="audit-list">
              {auditData.map((entry: AuditLogEntryDto, index: number) => (
                <Box key={entry.id}>
                  <ListItem alignItems="flex-start">
                    <ListItemText
                      primary={entry.action}
                      secondary={
                        <>
                          <Typography variant="body2" component="span" color="text.secondary">
                            {formatDate(entry.timestamp)}
                          </Typography>
                          {entry.fieldName && (
                            <Typography variant="body2" component="div">
                              Field: {entry.fieldName}
                              {entry.previousValue && ` (was: ${entry.previousValue})`}
                              {entry.newValue && ` -> ${entry.newValue}`}
                            </Typography>
                          )}
                        </>
                      }
                    />
                  </ListItem>
                  {index < auditData.length - 1 && <Divider />}
                </Box>
              ))}
            </List>
          ) : (
            <Typography color="text.secondary" align="center" sx={{ py: 4 }}>
              No audit entries
            </Typography>
          )}
        </Box>
      </Drawer>
    </Box>
  );
}

export default ReviewDetailPage;
