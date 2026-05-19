export interface SettingDescriptor {
  key: string;
  isSecret: boolean;
  isSet: boolean;
  value: string | null;
  description: string | null;
  updatedAt: string;
  updatedBy: string;
}
