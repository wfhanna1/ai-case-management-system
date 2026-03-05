import { useQuery } from '@tanstack/react-query';
import api from '@/services/api';
import type { HealthCheckResponse } from '@/types';

export function useHealthCheck() {
  return useQuery<HealthCheckResponse>({
    queryKey: ['health'],
    queryFn: async () => {
      const response = await api.get<HealthCheckResponse>('/health');
      return response.data;
    },
    refetchInterval: 30000,
    staleTime: 1000 * 20,
  });
}
