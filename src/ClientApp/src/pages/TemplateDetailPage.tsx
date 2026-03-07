import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useParams, useNavigate } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import Button from '@mui/material/Button';
import Chip from '@mui/material/Chip';
import Divider from '@mui/material/Divider';
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup';
import ToggleButton from '@mui/material/ToggleButton';
import ArrowBackIcon from '@mui/icons-material/ArrowBack';
import PrintIcon from '@mui/icons-material/Print';
import ListAltIcon from '@mui/icons-material/ListAlt';
import DescriptionIcon from '@mui/icons-material/Description';
import { getTemplateById, type TemplateField } from '@/services/templateService';

function fieldTypeLabel(type: string): string {
  const labels: Record<string, string> = {
    Text: 'Text Input',
    Date: 'Date Picker',
    Number: 'Number Input',
    Select: 'Dropdown',
    Checkbox: 'Checkbox',
    TextArea: 'Text Area',
  };
  return labels[type] ?? type;
}

function parseOptions(raw: string | null): string[] {
  if (!raw) return [];
  try {
    const parsed: unknown = JSON.parse(raw);
    if (Array.isArray(parsed) && parsed.every((item) => typeof item === 'string')) {
      return parsed;
    }
    return [];
  } catch {
    return [];
  }
}

function FieldPreview({ field }: { field: TemplateField }) {
  const options = parseOptions(field.options);

  return (
    <Box sx={{ py: 1.5 }}>
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, mb: 0.5 }}>
        <Typography fontWeight={600}>{field.label}</Typography>
        {field.isRequired && (
          <Chip label="Required" size="small" color="error" variant="outlined" />
        )}
      </Box>
      <Typography variant="body2" color="text.secondary">
        Type: {fieldTypeLabel(field.fieldType)}
      </Typography>
      {options.length > 0 && (
        <Box sx={{ mt: 0.5, display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
          {options.map((opt) => (
            <Chip key={opt} label={opt} size="small" variant="outlined" />
          ))}
        </Box>
      )}
    </Box>
  );
}

function PrintableField({ field }: { field: TemplateField }) {
  const options = parseOptions(field.options);
  const required = field.isRequired ? ' *' : '';

  if (field.fieldType === 'Checkbox') {
    return (
      <Box sx={{ py: 1, display: 'flex', alignItems: 'center', gap: 1 }} data-testid={`printable-field-${field.label}`}>
        <Box
          sx={{
            width: 18,
            height: 18,
            border: '2px solid #555',
            borderRadius: '3px',
            flexShrink: 0,
          }}
        />
        <Typography>{field.label}{required}</Typography>
      </Box>
    );
  }

  if (field.fieldType === 'Select' && options.length > 0) {
    return (
      <Box sx={{ py: 1 }} data-testid={`printable-field-${field.label}`}>
        <Typography sx={{ mb: 0.5 }}>{field.label}{required}</Typography>
        <Box sx={{ display: 'flex', gap: 2, flexWrap: 'wrap', ml: 1 }}>
          {options.map((opt) => (
            <Box key={opt} sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
              <Box
                sx={{
                  width: 14,
                  height: 14,
                  border: '2px solid #555',
                  borderRadius: '50%',
                  flexShrink: 0,
                }}
              />
              <Typography variant="body2">{opt}</Typography>
            </Box>
          ))}
        </Box>
      </Box>
    );
  }

  if (field.fieldType === 'TextArea') {
    return (
      <Box sx={{ py: 1 }} data-testid={`printable-field-${field.label}`}>
        <Typography sx={{ mb: 0.5 }}>{field.label}{required}</Typography>
        <Box
          sx={{
            border: '1px solid #ccc',
            borderRadius: '4px',
            height: 80,
            '@media print': { height: 100 },
          }}
        />
      </Box>
    );
  }

  return (
    <Box sx={{ py: 1 }} data-testid={`printable-field-${field.label}`}>
      <Typography sx={{ mb: 0.5 }}>{field.label}{required}</Typography>
      <Box
        sx={{
          borderBottom: '1px solid #999',
          height: 28,
        }}
      />
    </Box>
  );
}

function handlePrint() {
  window.print();
}

type ViewMode = 'structure' | 'form';

function TemplateDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [viewMode, setViewMode] = useState<ViewMode>('structure');
  const { data: template, isLoading, error } = useQuery({
    queryKey: ['template', id],
    queryFn: () => getTemplateById(id!),
    enabled: !!id,
  });

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error || !template) {
    return (
      <Box>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error ? 'Failed to load template.' : 'Template not found.'}
        </Alert>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate('/templates')}>
          Back to Templates
        </Button>
      </Box>
    );
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', mb: 3, '@media print': { display: 'none' } }}>
        <Box>
          <Button
            startIcon={<ArrowBackIcon />}
            onClick={() => navigate('/templates')}
            sx={{ mb: 1 }}
          >
            Back to Templates
          </Button>
          <Typography variant="h4" component="h1" fontWeight={700}>
            {template.name}
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ mt: 1 }}>
            {template.description}
          </Typography>
        </Box>
        <Button
          variant="outlined"
          startIcon={<PrintIcon />}
          onClick={handlePrint}
          data-testid="print-button"
        >
          Print / PDF
        </Button>
      </Box>

      <Box sx={{ display: 'flex', gap: 1, mb: 3, '@media print': { display: 'none' } }}>
        <Chip label={template.type} color="primary" />
        <Chip
          label={template.isActive ? 'Active' : 'Inactive'}
          color={template.isActive ? 'success' : 'default'}
        />
        <Chip label={`${template.fields.length} fields`} variant="outlined" />
      </Box>

      <Box sx={{ mb: 2, '@media print': { display: 'none' } }}>
        <ToggleButtonGroup
          value={viewMode}
          exclusive
          onChange={(_, v) => { if (v) setViewMode(v as ViewMode); }}
          size="small"
        >
          <ToggleButton value="structure" data-testid="view-structure-btn">
            <ListAltIcon sx={{ mr: 0.5 }} /> Structure
          </ToggleButton>
          <ToggleButton value="form" data-testid="view-form-btn">
            <DescriptionIcon sx={{ mr: 0.5 }} /> Printable Form
          </ToggleButton>
        </ToggleButtonGroup>
      </Box>

      {viewMode === 'structure' && (
        <Paper sx={{ p: 3, '@media print': { display: 'none' } }} elevation={1} data-testid="structure-view">
          <Typography variant="h6" gutterBottom fontWeight={600}>
            Field Structure
          </Typography>
          <Divider sx={{ mb: 2 }} />
          {template.fields.length === 0 ? (
            <Typography color="text.secondary">
              No fields defined for this template.
            </Typography>
          ) : (
            template.fields.map((field, index) => (
              <Box key={index}>
                <FieldPreview field={field} />
                {index < template.fields.length - 1 && <Divider />}
              </Box>
            ))
          )}
        </Paper>
      )}

      {viewMode === 'form' && (
        <Paper sx={{ p: 3 }} elevation={1} data-testid="form-view">
          <Typography variant="h5" gutterBottom fontWeight={700} sx={{ textAlign: 'center', mb: 1 }}>
            {template.name}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ textAlign: 'center', mb: 3 }}>
            {template.description}
          </Typography>
          <Divider sx={{ mb: 2 }} />
          {template.fields.map((field, index) => (
            <Box key={index}>
              <PrintableField field={field} />
            </Box>
          ))}
        </Paper>
      )}
    </Box>
  );
}

export default TemplateDetailPage;
