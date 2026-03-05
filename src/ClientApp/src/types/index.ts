export interface ApiError {
  message: string;
  statusCode: number;
  errors?: Record<string, string[]>;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface HealthCheckResponse {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  totalDuration: string;
  entries: Record<string, HealthCheckEntry>;
}

export interface HealthCheckEntry {
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description?: string;
  duration: string;
}
