import { CurrencyPipe, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import {
  forkJoin, switchMap, map, interval, Subscription, finalize, catchError, of, throwError
} from 'rxjs';

import { ParticipantCardComponent } from '@app/components/group-orders/participant-card/participant-card.component';
import { GuestInvite } from '@app/models/reservations/guest-invite.model';
import { GroupSessionsResponse } from '@app/models/group-orders/group-sessions-response.model';
import { GroupStatusResponse } from '@app/models/group-orders/group-status.model';
import { GroupOrdersService } from '@app/services/group-orders/group-orders.service';
import { ToastService } from '@app/services/toast/toast.service';
import { consolidatePreOrder } from '@app/shared/helpers/consolidate-pre-order';
import { ModalComponent } from '@app/shared/custom-components/modal/modal.component';

@Component({
  selector: 'app-group-order-page',
  imports: [ParticipantCardComponent, CurrencyPipe, DatePipe, ModalComponent],
  templateUrl: './group-order-page.component.html',
  styleUrl: './group-order-page.component.scss',
  changeDetection: ChangeDetectionStrategy.Eager,
})
export class GroupOrderPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(GroupOrdersService);
  private readonly toast = inject(ToastService);
  private readonly destroyRef = inject(DestroyRef);
private polling?: Subscription;

readonly lastUpdated = signal<Date | null>(null);
readonly refreshError = signal(false);
readonly refreshing = signal(false);

  readonly reservationId =
    this.route.snapshot.paramMap.get('reservationId') ?? '';

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly invite = signal<GuestInvite | null>(null);
  readonly sessions = signal<GroupSessionsResponse | null>(null);
  readonly status = signal<GroupStatusResponse | null>(null);
  readonly ownerSessionId = signal('');

  readonly confirmOpen = signal(false);
  readonly submitting = signal(false);

  readonly participants = computed(
    () => this.sessions()?.participants ?? []
  );

readonly displayParticipants = computed(() =>
  [...this.participants()].sort(
    (a, b) => Number(b.type === 'Owner') - Number(a.type === 'Owner')
  )
);

readonly confirmationTotal = computed(() =>
  this.participants()
    .filter(person => person.isSubmitted)
    .reduce((sum, person) => sum + person.total, 0)
);

  readonly consolidated = computed(
    () => consolidatePreOrder(this.participants())
  );

  readonly pendingCount = computed(
    () => this.participants().filter(p => !p.isSubmitted).length
  );

  readonly isEmpty = computed(
    () => this.participants().every(p => p.items.length === 0)
  );

  readonly isSubmitted = computed(
    () => this.status()?.status === 'Submitted'
  );

  readonly canSubmit = computed(
    () =>
      this.status()?.status === 'Open' &&
      this.consolidated().items.length > 0 &&
      !this.submitting()
  );

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (!this.reservationId) {
      this.loading.set(false);
      this.error.set('Group reservation not found');
      return;
    }

    this.loading.set(true);
    this.error.set(null);

    this.service.getGroupInvite$(this.reservationId).pipe(
      // After submission the invitation endpoint returns HTTP 400.
      // Only in that case, load the completed order through the owner API.
      catchError((err: HttpErrorResponse) =>
        err.status === 400 ? of(null) : throwError(() => err)
      ),
      switchMap(invite => {
        if (invite) {
          return forkJoin({
            sessions: this.service.getSessions$(invite.GroupReservationId),
            ownerId: this.service.getOwnerSessionId$(this.reservationId),
            status: this.service.getStatus$(invite.GroupReservationId),
          }).pipe(map(result => ({ invite, ...result })));
        }

        return this.service.getOwnerInfo$(this.reservationId).pipe(
          switchMap(owner => {
            if (!owner.groupReservationId) {
              return throwError(() => new Error('Missing groupReservationId'));
            }
            return forkJoin({
              sessions: this.service.getSessions$(owner.groupReservationId),
              status: this.service.getStatus$(owner.groupReservationId),
              booking: this.service.getReservationDetails$(this.reservationId),
            }).pipe(
              map(({ sessions, status, booking }) => ({
                invite: {
                  GroupReservationId: owner.groupReservationId,
                  LocationId: '',
                  RestaurantName: booking.restaurantName,
                  LocationAddress: booking.locationAddress,
                  StartTime: String(booking.startTime),
                  EndTime: String(booking.endTime),
                  MaxGuestCount: booking.guestCount,
                  TableNumber: 0,
                  InviterName: '',
                } satisfies GuestInvite,
                sessions,
                status,
                ownerId: owner.guestSessionId,
              }))
            );
          })
        );
      }),
      takeUntilDestroyed(this.destroyRef)
    )
     .subscribe({
      next: ({ invite, sessions, ownerId, status }) => {
        this.invite.set(invite);
        this.sessions.set(sessions);
        this.ownerSessionId.set(ownerId);
        this.status.set(status);
        this.loading.set(false);
this.lastUpdated.set(new Date());
this.refreshError.set(false);
this.startSync();
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(
          err.status === 404
            ? 'Group reservation not found'
            : 'Failed to load group pre-order'
        );
        this.loading.set(false);
      },
    });
  }

  openSubmitDialog(): void {
    if (this.canSubmit()) {
      this.confirmOpen.set(true);
    }
  }

  closeSubmitDialog(): void {
    if (!this.submitting()) {
      this.confirmOpen.set(false);
    }
  }

  submit(): void {
    const groupId = this.invite()?.GroupReservationId;

    if (!groupId || !this.canSubmit() || !this.confirmOpen()) {
      return;
    }

    this.submitting.set(true);

    this.service.submit$(groupId, this.reservationId).pipe(
      takeUntilDestroyed(this.destroyRef)
).subscribe({
  next: () => {
    this.polling?.unsubscribe();
    const currentStatus = this.status();

        if (currentStatus) {
          this.status.set({
            ...currentStatus,
            status: 'Submitted',
          });
        }

        this.submitting.set(false);
        this.confirmOpen.set(false);
        this.toast.success('Pre-order sent to the restaurant');
      },
      error: () => {
        this.submitting.set(false);
        this.confirmOpen.set(false);
      },
    });
  }

private startSync(): void {
  if (this.polling && !this.polling.closed) {
    return;
  }

  if (this.status()?.status !== 'Open') {
    return;
  }

  this.polling = interval(15_000)
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe(() => {
      if (this.status()?.status !== 'Open') {
        this.polling?.unsubscribe();
        return;
      }

      this.refresh();
    });
}

refresh(): void {
  const groupId = this.invite()?.GroupReservationId;

  if (
    !groupId ||
    this.status()?.status !== 'Open' ||
    this.refreshing()
  ) {
    return;
  }

  this.refreshing.set(true);

  forkJoin({
    sessions: this.service.getSessions$(groupId, { silent: true }),
    status: this.service.getStatus$(groupId, { silent: true }),
  }).pipe(
    takeUntilDestroyed(this.destroyRef),
    finalize(() => this.refreshing.set(false))
  ).subscribe({
    next: ({ sessions, status }) => {
      if (this.isSubmitted()) {
        return;
      }

      this.sessions.set(sessions);
      this.status.set(status);
      this.lastUpdated.set(new Date());
      this.refreshError.set(false);

      if (status.status !== 'Open') {
        this.polling?.unsubscribe();
      }
    },
    error: () => {
      this.refreshError.set(true);
    },
  });
}

}
