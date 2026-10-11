import { describe, expect, it } from 'vitest';
import {
  REASON_MAX_LENGTH,
  REASON_MIN_LENGTH,
  escalationBlockedReason,
  validateEscalation,
} from './escalationRules';

describe('validateEscalation', () => {
  it('accepts a normal reason', () => {
    expect(validateEscalation('Customer is losing money')).toEqual({});
  });

  it('counts the reason after trimming, with the same limits as the API', () => {
    expect(validateEscalation('x'.repeat(REASON_MIN_LENGTH - 1)).reason).toBeDefined();
    expect(validateEscalation('x'.repeat(REASON_MIN_LENGTH)).reason).toBeUndefined();
    expect(validateEscalation('x'.repeat(REASON_MAX_LENGTH)).reason).toBeUndefined();
    expect(validateEscalation('x'.repeat(REASON_MAX_LENGTH + 1)).reason).toBeDefined();
    expect(validateEscalation('   abcd   ').reason).toBeDefined();
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
