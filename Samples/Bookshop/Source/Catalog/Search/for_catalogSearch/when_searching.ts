import { describe, it, beforeEach } from 'vitest';

describe('when searching by part of an author name', () => {
    beforeEach(() => catalog.withBooksBy('Ursula K. Le Guin', 'Octavia E. Butler'));

    it('shows every matching title', () => {
        results.should.contain('The Dispossessed');
        results.should.contain('A Wizard of Earthsea');
    });

    it('keeps the author visible beside each title', () => {
        results[0].author.should.equal('Ursula K. Le Guin');
    });

    it('does not show books by another author', () => {
        results.should.not.contain('Kindred');
    });
});
