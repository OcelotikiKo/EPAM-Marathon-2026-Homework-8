import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { SKIP_ERROR_TOAST } from '@app/auth/skip-error-toast.context';
import { Participant } from '@app/models/group-orders/participant.model';
import { ReservationCustomer } from '@app/models/reservations/reservation.model';

import { API_BASE_URL } from '@app/shared/config/api-base-url.token';
import { GroupInviteToken, GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { OwnerSessionResponse } from '@app/models/group-orders/owner-session.model';
import { SubmittedDish } from '@app/models/group-orders/submit-group-pre-order.model';

type ApiSessionsResponse = {
  Participants: {
    Id: string;
    Type: Participant['type'];
    Name: string;
    Items: Participant['items'];
    Total: number;
    IsSubmitted: boolean;
  }[];
  Total: number;
};

@Injectable({
  providedIn: 'root',
})
export class GroupOrdersService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  getGroupInvite$(reservationId: string): Observable<GuestInvite> {
    return this.http.get<GroupInviteToken>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/invite-token`,
      { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }
    ).pipe(
      switchMap(({ token }) =>
        this.http.get<GuestInvite>(
          `${this.baseUrl}/guest/invites/${encodeURIComponent(token)}`,
          { context: new HttpContext().set(SKIP_ERROR_TOAST, true) }
        )
      )
    );
  }


getSessions$(
  groupReservationId: string,
  options: { silent?: boolean } = {}
): Observable<GroupSessionsResponse> {
  return this.http.get<GroupSessionsResponse | ApiSessionsResponse>(
    `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/sessions`,
    {
      context: new HttpContext().set(
        SKIP_ERROR_TOAST,
        options.silent === true
      ),
    }
  ).pipe(
    map(response => {
      if (!('Participants' in response)) {
        return response;
      }

      return {
        participants: response.Participants.map(person => ({
          id: person.Id,
          type: person.Type,
          name: person.Name,
          items: person.Items,
          total: person.Total,
          isSubmitted: person.IsSubmitted
        })),
        total: response.Total
      };
    })
  );
}

  getOwnerSessionId$(reservationId: string): Observable<string> {
    return this.http.get<OwnerSessionResponse>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`
    ).pipe(
      map(response => response.guestSessionId)
    );
  }

  getOwnerInfo$(reservationId: string): Observable<OwnerSessionResponse> {
    return this.http.get<OwnerSessionResponse>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}/guest-session-id`
    );
  }

  getReservationDetails$(reservationId: string): Observable<ReservationCustomer> {
    return this.http.get<ReservationCustomer>(
      `${this.baseUrl}/reservations/${encodeURIComponent(reservationId)}`
    );
  }

getStatus$(
  groupReservationId: string,
  options: { silent?: boolean } = {}
): Observable<GroupStatusResponse> {
  return this.http.get<GroupStatusResponse>(
    `${this.baseUrl}/guest/groups/${encodeURIComponent(groupReservationId)}/status`,
    {
      context: new HttpContext().set(
        SKIP_ERROR_TOAST,
        options.silent === true
      ),
    }
  );
}

  submit$(
    groupReservationId: string,
    reservationId: string
  ): Observable<SubmittedDish[]> {
    return this.http.post<SubmittedDish[]>(
      `${this.baseUrl}/group-reservations/${encodeURIComponent(groupReservationId)}/submit`,
      { reservationId }
    );
  }
}
