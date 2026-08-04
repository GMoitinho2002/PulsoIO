export type EnvironmentKind = 'Production' | 'Staging' | 'Development';
export type IntegrationDirection = 'Inbound' | 'Outbound' | 'Bidirectional';

export interface ClientSummary {
  id: string;
  name: string;
  isActive: boolean;
  environmentCount: number;
  integrationCount: number;
}

export interface ClientEnvironment {
  id: string;
  name: string;
  kind: EnvironmentKind;
  isActive: boolean;
}

export interface ClientIntegration {
  id: string;
  name: string;
  environmentId: string;
  direction: IntegrationDirection;
  sourceSystem: string;
  targetSystem: string;
  httpMethod: string | null;
  endpointPattern: string | null;
  isActive: boolean;
}

export interface ClientDetail {
  id: string;
  name: string;
  isActive: boolean;
  createdAtUtc?: string;
  updatedAtUtc?: string;
  environments: ClientEnvironment[];
  integrations: ClientIntegration[];
}

export interface SaveClientRequest {
  name: string;
  isActive: boolean;
}

export interface SaveEnvironmentRequest {
  name: string;
  kind: EnvironmentKind;
  isActive: boolean;
}

export interface SaveIntegrationRequest {
  name: string;
  environmentId: string;
  direction: IntegrationDirection;
  sourceSystem: string;
  targetSystem: string;
  httpMethod: string | null;
  endpointPattern: string | null;
  isActive: boolean;
}

