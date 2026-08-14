import { describe, it } from 'vitest';

describe('when the email address is invalid', () => {
    it('keeps the register action disabled', () => {
        registration.registerEnabled.should.equal(false);
    });

    it('explains how to correct the address', () => {
        registration.emailError.should.equal('Enter a valid email address');
    });
});
