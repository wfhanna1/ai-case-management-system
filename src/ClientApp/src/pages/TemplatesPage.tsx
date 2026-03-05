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
import CircularProgress from '@mui/material/CircularProgress';
import Alert from '@mui/material/Alert';
import { getTemplates, type FormTemplate } from '@/services/templateService';

function templateTypeLabel(type: string): string {
  const labels: Record<string, string> = {
    ChildWelfare: 'Child Welfare',
    AdultProtective: 'Adult Protective',
    HousingAssistance: 'Housing Assistance',
    MentalHealthReferral: 'Mental Health Referral',
  };
  return labels[type] ?? type;
}

function TemplatesPage() {
  const navigate = useNavigate();
  const { data: templates, isLoading, error } = useQuery<FormTemplate[]>({
    queryKey: ['templates'],
    queryFn: getTemplates,
  });

  return (
    <Box>
      <Typography variant="h4" component="h1" gutterBottom fontWeight={700}>
        Form Templates
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Browse and preview available intake form templates.
      </Typography>

      {isLoading && (
        <Box sx={{ display: 'flex', justifyContent: 'center', py: 4 }}>
          <CircularProgress />
        </Box>
      )}

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          Failed to load templates. Please try again.
        </Alert>
      )}

      {templates && templates.length === 0 && (
        <Paper sx={{ p: 3 }} elevation={1}>
          <Typography color="text.secondary">
            No templates available yet.
          </Typography>
        </Paper>
      )}

      {templates && templates.length > 0 && (
        <TableContainer component={Paper} elevation={1}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Type</TableCell>
                <TableCell>Fields</TableCell>
                <TableCell>Status</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {templates.map((template) => (
                <TableRow
                  key={template.id}
                  hover
                  sx={{ cursor: 'pointer' }}
                  onClick={() => navigate(`/templates/${template.id}`)}
                  data-testid={`template-row-${template.id}`}
                >
                  <TableCell>
                    <Typography fontWeight={600}>{template.name}</Typography>
                    <Typography variant="body2" color="text.secondary">
                      {template.description}
                    </Typography>
                  </TableCell>
                  <TableCell>{templateTypeLabel(template.type)}</TableCell>
                  <TableCell>{template.fields.length}</TableCell>
                  <TableCell>
                    <Chip
                      label={template.isActive ? 'Active' : 'Inactive'}
                      color={template.isActive ? 'success' : 'default'}
                      size="small"
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}

export default TemplatesPage;
