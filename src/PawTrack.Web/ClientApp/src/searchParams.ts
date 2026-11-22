import { useSearchParams } from 'react-router-dom';

// Keeps page and filter state in the URL so pages can be bookmarked and shared, as with the old Razor pages.
export function usePageFilters<K extends string>(filterKeys: readonly K[]) {
  const [params, setParams] = useSearchParams();

  const page = Math.max(Number(params.get('page')) || 1, 1);
  const filters = Object.fromEntries(filterKeys.map((k) => [k, params.get(k) ?? ''])) as Record<K, string>;

  const update = (changes: Partial<Record<K | 'page', string | number>>) => {
    const next = new URLSearchParams(params);
    for (const [key, value] of Object.entries(changes)) {
      if (value === '' || value === undefined || (key === 'page' && value === 1)) next.delete(key);
      else next.set(key, String(value));
    }
    setParams(next);
  };

  return {
    page,
    filters,
    setPage: (p: number) => update({ page: p } as Partial<Record<K | 'page', number>>),
    // Changing a filter returns to the first page.
    setFilter: (key: K, value: string) => update({ [key]: value, page: 1 } as Partial<Record<K | 'page', string | number>>),
  };
}
