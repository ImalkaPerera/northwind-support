import { describe, expect, it } from 'vitest';
import {
  ESCALATED_BY_MAX_LENGTH,
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlockedReason,
  validateEscalation,
} from './escalationRules';

describe('validateEscalation', () => {
  it('accepts a normal request', () => {
    expect(validateEscalation({ reason: 'Customer is losing money', escalatedBy: 'Alex' })).toEqual({});
  });

  it('counts the reason after trimming, with the same limits as the API', () => {
    const at = (length: number) => ({ reason: 'x'.repeat(length), escalatedBy: 'Alex' });

    expect(validateEscalation(at(REASON_MIN_LENGTH - 1)).reason).toBeDefined();
    expect(validateEscalation(at(REASON_MIN_LENGTH)).reason).toBeUndefined();
    expect(validateEscalation(at(REASON_MAX_LENGTH)).reason).toBeUndefined();
    expect(validateEscalation(at(REASON_MAX_LENGTH + 1)).reason).toBeDefined();
    expect(validateEscalation({ reason: '   abcd   ', escalatedBy: 'Alex' }).reason).toBeDefined();
  });

  it('requires who is escalating, up to the maximum length', () => {
    expect(validateEscalation({ reason: 'Customer is losing money', escalatedBy: '   ' }).escalatedBy).toBeDefined();
    expect(
      validateEscalation({
        reason: 'Customer is losing money',
        escalatedBy: 'x'.repeat(ESCALATED_BY_MAX_LENGTH + 1),
      }).escalatedBy,
    ).toBeDefined();
  });
});

describe('escalationBlockedReason', () => {
  it('allows an open ticket below Critical', () => {
    expect(escalationBlockedReason({ status: 'InProgress', priority: 'High' })).toBeUndefined();
  });

  it('blocks Critical, Resolved and Closed tickets and says which', () => {
    expect(escalationBlockedReason({ status: 'Open', priority: 'Critical' })).toMatch(/Critical/);
    expect(escalationBlockedReason({ status: 'Resolved', priority: 'Low' })).toMatch(/Resolved/);
    expect(escalationBlockedReason({ status: 'Closed', priority: 'Low' })).toMatch(/Closed/);
  });
});
