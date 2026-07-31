import { api } from './client.js';

// GET /api/drivers
// Returns: DriverDto[] { id, name, phone, isAvailable }
export function getDrivers() {
  return api.get('/api/drivers');
}
