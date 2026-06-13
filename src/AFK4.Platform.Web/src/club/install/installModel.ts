export function getSetupMsiUrl(): string {
  const configured = import.meta.env.VITE_SETUP_MSI_URL;
  return typeof configured === 'string' && configured.trim().length > 0
    ? configured.trim()
    : '/downloads/AFK4-Agent.msi';
}
