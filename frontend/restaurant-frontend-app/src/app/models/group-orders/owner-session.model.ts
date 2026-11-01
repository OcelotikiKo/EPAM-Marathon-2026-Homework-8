/** GET /reservations/{reservationId}/guest-session-id */
export interface OwnerSessionResponse {
  guestSessionId: string;
  groupReservationId: string;
}
