import { useEffect, useRef, useState } from 'react';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import type { OperatorBackendContext } from './operatorTypes';

// Which platform features are enabled for the organization, so destinations can hide the setup UI
// for a feature that's off (configuring something disabled is pointless). Loaded once per backend
// context, not per destination — cheap and shared.
//
// `null` means "not loaded yet, or the list failed to load" — every feature is treated as enabled
// in that state. This is UI convenience, not a security boundary: the server enforces the gate on
// its own for every write/read that matters, regardless of what the Operator renders. Hiding a
// working section because of a network hiccup would be worse than briefly showing one whose
// action then gets refused server-side.
export function useOrganizationFeatures(backend: OperatorBackendContext | null): string[] | null {
  const [features, setFeatures] = useState<string[] | null>(null);
  const fetchedForRef = useRef<string | null>(null);

  useEffect(() => {
    if (backend === null) {
      fetchedForRef.current = null;
      setFeatures(null);
      return;
    }
    if (fetchedForRef.current === backend.session.organizationId) return;
    fetchedForRef.current = backend.session.organizationId;

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.features.list()
      .then(setFeatures)
      .catch(() => { /* fail open, see comment above */ });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [backend?.config, backend?.session]);

  return features;
}
