import type {
  EscalationResult,
  PagedResult,
  RaisedTicket,
  TicketDetail,
  TicketEscalation,
  TicketListItem,
} from '../types/api';

/** A list row with sensible defaults, so tests only state what they care about. */
export function ticketListItem(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    status: 'InProgress',
    priority: 'High',
    customer: { id: 2, name: 'Fabrikam Inc', tier: 'Standard' },
    category: { id: 2, name: 'Billing' },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'Breached',
    ...overrides,
  };
}

export function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return {
    items,
    page: 1,
    pageSize: 20,
    totalCount: items.length,
    totalPages: 1,
  };
}

/** A ticket detail with sensible defaults. */
export function ticketDetail(overrides: Partial<TicketDetail> = {}): TicketDetail {
  return {
    id: 12,
    reference: 'TCK-0012',
    title: 'Invoice total does not match the order',
    description: 'Invoice 5512 is EUR 240 higher than the confirmed order.',
    status: 'InProgress',
    priority: 'High',
    customer: {
      id: 2,
      name: 'Fabrikam Inc',
      tier: 'Standard',
      email: 'helpdesk@fabrikam.example',
      phone: null,
    },
    category: { id: 2, name: 'Billing', requiresSpecialist: true },
    assignedAgent: { id: 4, fullName: 'Sara Lindqvist' },
    createdAtUtc: '2026-09-24T08:12:00Z',
    updatedAtUtc: '2026-09-25T10:03:00Z',
    dueAtUtc: '2026-09-24T16:12:00Z',
    resolvedAtUtc: null,
    slaStatus: 'WithinSla',
    ...overrides,
  };
}

export function escalation(overrides: Partial<TicketEscalation> = {}): TicketEscalation {
  return {
    id: 1,
    ticketId: 12,
    fromPriority: 'Medium',
    toPriority: 'High',
    fromAgent: { id: 4, fullName: 'Sara Lindqvist' },
    toAgent: { id: 1, fullName: 'Alex Turner' },
    fromDueAtUtc: '2026-09-25T08:00:00Z',
    toDueAtUtc: '2026-09-25T16:00:00Z',
    reason: 'Customer is losing money every hour',
    escalatedBy: 'Alex Turner',
    escalatedAtUtc: '2026-09-25T09:00:00Z',
    ...overrides,
  };
}

export function escalationResult(overrides: Partial<EscalationResult> = {}): EscalationResult {
  return {
    ticket: ticketDetail({ priority: 'Critical' }),
    escalation: escalation(),
    decision: {
      priorityReason: 'Raised from High to Critical.',
      slaWindowReason: 'Due 4 hours after escalation: Critical tickets get 4 hours.',
      assignmentReason: 'Kept with Sara Lindqvist, who is still eligible.',
    },
    ...overrides,
  };
}

export function raisedTicket(overrides: Partial<RaisedTicket> = {}): RaisedTicket {
  return {
    ...ticketDetail(),
    triage: {
      priorityReason: 'The requested priority (High) was used.',
      slaWindowReason: 'Due 8 hours after creation: High tickets get 8 hours.',
      assignmentReason: 'Assigned to Sara Lindqvist: the eligible agent with the fewest open tickets (2).',
    },
    ...overrides,
  };
}
