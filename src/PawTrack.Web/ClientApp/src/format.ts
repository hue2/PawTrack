const currency = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' });
const number = new Intl.NumberFormat('en-US');

export const formatCurrency = (value: number) => currency.format(value);
export const formatNumber = (value: number) => number.format(value);
// The API returns UTC timestamps; the date part is all the tables show.
export const formatDate = (value: string | null) => (value ? value.slice(0, 10) : '-');

export const SPECIES = ['Dog', 'Cat', 'Rabbit', 'Bird', 'Reptile'];
export const CITIES = ['Austin', 'Denver', 'Seattle', 'Portland', 'Chicago', 'Boston', 'Atlanta', 'Phoenix', 'San Diego', 'Miami'];
export const PAGE_SIZE = 500;
