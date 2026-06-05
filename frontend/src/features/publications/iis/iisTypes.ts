// MLC-047 (ADR-24): типы управления жизненным циклом IIS. State приходит строковым
// именем enum'а IisObjectState (backend JsonStringEnumConverter).
export type IisObjectState = "Unknown" | "Starting" | "Started" | "Stopping" | "Stopped";

export interface IisAppPool {
  name: string;
  state: IisObjectState;
}

export interface IisSiteState {
  siteName: string;
  state: IisObjectState;
}

// Общий конверт discovery (зеркало backend DiscoveryResponse<T>). available:false —
// IIS недоступен/нет прав; error несёт санитизированный русский текст для показа.
export interface IisDiscoveryResponse<T> {
  items: T[];
  available: boolean;
  error: string | null;
}

export interface IisOperationResponse {
  name: string;
  state: IisObjectState;
}

// Состояние IIS в целом (служба W3SVC). available:false — статус не прочитан.
export interface IisServerStatus {
  state: IisObjectState;
  available: boolean;
  error: string | null;
}
