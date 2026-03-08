import { useState, useMemo, useCallback } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { searchDocuments, type SearchDocumentsParams, type SearchDocumentsResult } from '@/services/documentService';

export interface UseDocumentSearchReturn {
  fileName: string;
  setFileName: (v: string) => void;
  status: string;
  setStatus: (v: string) => void;
  fieldValue: string;
  setFieldValue: (v: string) => void;
  fromDate: string;
  setFromDate: (v: string) => void;
  toDate: string;
  setToDate: (v: string) => void;
  page: number;
  setPage: (v: number) => void;
  pageSize: number;
  setPageSize: (v: number) => void;
  searchTriggered: boolean;
  handleSearch: () => void;
  handleClear: () => void;
  committedParams: SearchDocumentsParams;
  dateError: string | null;
  data: SearchDocumentsResult | undefined;
  isLoading: boolean;
  isError: boolean;
  error: Error | null;
}

export function useDocumentSearch(): UseDocumentSearchReturn {
  const queryClient = useQueryClient();

  const [fileName, setFileName] = useState('');
  const [status, setStatus] = useState('');
  const [fieldValue, setFieldValue] = useState('');
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [page, setPageRaw] = useState(0);
  const [pageSize, setPageSizeRaw] = useState(20);
  const [searchTriggered, setSearchTriggered] = useState(false);

  // Committed params: snapshot of filters at the time search was clicked.
  // This prevents query key changes on every keystroke.
  const [committedParams, setCommittedParams] = useState<SearchDocumentsParams>({
    page: 1,
    pageSize: 20,
  });

  const dateError = useMemo(() => {
    if (fromDate && toDate && fromDate > toDate) {
      return 'From date must be before To date';
    }
    return null;
  }, [fromDate, toDate]);

  const handleSearch = useCallback(() => {
    setPageRaw(0);
    setSearchTriggered(true);
    // Build params with page reset to 0
    const params: SearchDocumentsParams = {
      ...(fileName && { fileName }),
      ...(status && { status }),
      ...(fieldValue && { fieldValue }),
      ...(fromDate && { from: new Date(fromDate + 'T00:00:00').toISOString() }),
      ...(toDate && { to: new Date(toDate + 'T23:59:59.999').toISOString() }),
      page: 1,
      pageSize,
    };
    setCommittedParams(params);
  }, [fileName, status, fieldValue, fromDate, toDate, pageSize]);

  const handleClear = useCallback(() => {
    setFileName('');
    setStatus('');
    setFieldValue('');
    setFromDate('');
    setToDate('');
    setPageRaw(0);
    setPageSizeRaw(20);
    setSearchTriggered(false);
    setCommittedParams({ page: 1, pageSize: 20 });
    queryClient.removeQueries({ queryKey: ['searchDocuments'] });
  }, [queryClient]);

  const setPage = useCallback((newPage: number) => {
    setPageRaw(newPage);
    setCommittedParams(prev => ({ ...prev, page: newPage + 1 }));
  }, []);

  const setPageSize = useCallback((size: number) => {
    setPageSizeRaw(size);
    setPageRaw(0);
    setCommittedParams(prev => ({ ...prev, pageSize: size, page: 1 }));
  }, []);

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['searchDocuments', committedParams],
    queryFn: () => searchDocuments(committedParams),
    enabled: searchTriggered,
  });

  return {
    fileName,
    setFileName,
    status,
    setStatus,
    fieldValue,
    setFieldValue,
    fromDate,
    setFromDate,
    toDate,
    setToDate,
    page,
    setPage,
    pageSize,
    setPageSize,
    searchTriggered,
    handleSearch,
    handleClear,
    committedParams,
    dateError,
    data,
    isLoading,
    isError,
    error: error as Error | null,
  };
}
