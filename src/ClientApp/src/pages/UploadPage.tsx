import { useState, useCallback, type FormEvent, type DragEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation } from '@tanstack/react-query';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Button from '@mui/material/Button';
import Alert from '@mui/material/Alert';
import MenuItem from '@mui/material/MenuItem';
import TextField from '@mui/material/TextField';
import CircularProgress from '@mui/material/CircularProgress';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import InsertDriveFileIcon from '@mui/icons-material/InsertDriveFile';
import { getTemplates } from '@/services/templateService';
import { uploadDocument } from '@/services/documentService';

function UploadPage() {
  const navigate = useNavigate();
  const [file, setFile] = useState<File | null>(null);
  const [templateId, setTemplateId] = useState('');
  const [dragOver, setDragOver] = useState(false);
  const [error, setError] = useState('');

  const { data: templates = [] } = useQuery({
    queryKey: ['templates'],
    queryFn: getTemplates,
  });

  const mutation = useMutation({
    mutationFn: () => uploadDocument(file!, templateId || undefined),
    onError: () => setError('Upload failed. Please try again.'),
  });

  const handleDragOver = useCallback((e: DragEvent) => {
    e.preventDefault();
    setDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: DragEvent) => {
    e.preventDefault();
    setDragOver(false);
  }, []);

  const handleDrop = useCallback((e: DragEvent) => {
    e.preventDefault();
    setDragOver(false);
    const droppedFile = e.dataTransfer.files[0];
    if (droppedFile) {
      setFile(droppedFile);
      setError('');
    }
  }, []);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (selected) {
      setFile(selected);
      setError('');
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) {
      setError('Please select a file to upload.');
      return;
    }
    setError('');
    mutation.mutate();
  };

  if (mutation.isSuccess) {
    return (
      <Box sx={{ maxWidth: 600, mx: 'auto', mt: 4 }}>
        <Paper sx={{ p: 4, textAlign: 'center' }} elevation={2}>
          <CheckCircleIcon sx={{ fontSize: 64, color: 'success.main', mb: 2 }} />
          <Typography variant="h5" gutterBottom fontWeight={700}>
            Upload Successful
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
            Your document has been submitted for processing.
          </Typography>
          <Box sx={{ display: 'flex', gap: 2, justifyContent: 'center' }}>
            <Button variant="contained" onClick={() => navigate('/documents')}>
              View Documents
            </Button>
            <Button
              variant="outlined"
              onClick={() => {
                mutation.reset();
                setFile(null);
                setTemplateId('');
              }}
            >
              Upload Another
            </Button>
          </Box>
        </Paper>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: 600, mx: 'auto', mt: 4 }}>
      <Typography variant="h5" component="h1" gutterBottom fontWeight={700}>
        Upload Document
      </Typography>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {mutation.isError && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {mutation.error instanceof Error ? mutation.error.message : 'Upload failed'}
        </Alert>
      )}

      <Paper sx={{ p: 4 }} elevation={2}>
        <Box component="form" noValidate onSubmit={handleSubmit}>
          <TextField
            select
            label="Template (optional)"
            value={templateId}
            onChange={e => setTemplateId(e.target.value)}
            fullWidth
            margin="normal"
          >
            <MenuItem value="">None</MenuItem>
            {templates.map(t => (
              <MenuItem key={t.id} value={t.id}>
                {t.name}
              </MenuItem>
            ))}
          </TextField>

          <Box
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            sx={{
              mt: 2,
              p: 4,
              border: '2px dashed',
              borderColor: dragOver ? 'primary.main' : 'divider',
              borderRadius: 2,
              backgroundColor: dragOver ? 'action.hover' : 'background.default',
              textAlign: 'center',
              cursor: 'pointer',
              transition: 'all 0.2s',
            }}
            onClick={() => document.getElementById('file-input')?.click()}
          >
            <input
              id="file-input"
              type="file"
              hidden
              onChange={handleFileChange}
              aria-label="file input"
            />
            {file ? (
              <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 1 }}>
                <InsertDriveFileIcon color="primary" />
                <Typography>{file.name}</Typography>
              </Box>
            ) : (
              <>
                <CloudUploadIcon sx={{ fontSize: 48, color: 'text.secondary', mb: 1 }} />
                <Typography color="text.secondary">
                  Drag and drop a file here, or click to browse
                </Typography>
              </>
            )}
          </Box>

          <Button
            type="submit"
            variant="contained"
            fullWidth
            sx={{ mt: 3 }}
            size="large"
            disabled={mutation.isPending || !file}
            startIcon={mutation.isPending ? <CircularProgress size={20} color="inherit" /> : <CloudUploadIcon />}
          >
            {mutation.isPending ? 'Uploading...' : 'Upload'}
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}

export default UploadPage;
