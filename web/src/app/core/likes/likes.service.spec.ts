import { TestBed } from '@angular/core/testing';
import { LIKES_KEY, LikesService } from './likes.service';

describe('LikesService', () => {
  const create = (): LikesService => {
    TestBed.resetTestingModule();
    return TestBed.inject(LikesService);
  };
  const stored = (): unknown => JSON.parse(localStorage.getItem(LIKES_KEY) ?? 'null');

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it('starts with nothing liked', () => {
    const likes = create();

    expect(likes.count()).toBe(0);
    expect(likes.isLiked('p-1')).toBe(false);
  });

  it('toggles a like on and off, saving it in localStorage each time', () => {
    const likes = create();

    expect(likes.toggle('p-1')).toBe(true);
    expect(likes.isLiked('p-1')).toBe(true);
    expect(stored()).toEqual(['p-1']);

    expect(likes.toggle('p-1')).toBe(false);
    expect(likes.isLiked('p-1')).toBe(false);
    expect(stored()).toEqual([]);
  });

  it('remembers the likes after the page is reloaded (a new service instance)', () => {
    const first = create();
    first.toggle('p-1');
    first.toggle('p-2');

    const reloaded = create();

    expect(reloaded.isLiked('p-1')).toBe(true);
    expect(reloaded.isLiked('p-2')).toBe(true);
    expect(reloaded.isLiked('p-3')).toBe(false);
    expect(reloaded.count()).toBe(2);
  });

  it('does not depend on a session: it never touches the session key', () => {
    localStorage.setItem('alhecasa.session', 'x');
    const likes = create();

    likes.toggle('p-1');

    expect(localStorage.getItem('alhecasa.session')).toBe('x');
  });

  it.each([
    ['not JSON', '{oops'],
    ['not an array', '{"a":1}'],
    ['a string', '"p-1"'],
  ])('starts empty when the stored value is %s', (_name, raw) => {
    localStorage.setItem(LIKES_KEY, raw);

    expect(create().count()).toBe(0);
  });

  it('keeps only the valid, distinct ids from what was stored', () => {
    localStorage.setItem(LIKES_KEY, JSON.stringify(['p-1', 7, null, '', 'p-1', 'x'.repeat(65), { id: 'p-2' }, 'p-3']));

    const likes = create();

    expect(likes.count()).toBe(2);
    expect(likes.isLiked('p-1')).toBe(true);
    expect(likes.isLiked('p-3')).toBe(true);
  });

  it('caps the list at 100, dropping the oldest', () => {
    localStorage.setItem(LIKES_KEY, JSON.stringify(Array.from({ length: 100 }, (_, i) => `id-${i}`)));
    const likes = create();

    likes.toggle('nuevo');

    expect(likes.count()).toBe(100);
    expect(likes.isLiked('id-0')).toBe(false);
    expect(likes.isLiked('id-1')).toBe(true);
    expect(likes.isLiked('nuevo')).toBe(true);
  });

  it('still marks the like for this tab when the browser refuses to save', () => {
    const likes = create();
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError');
    });

    expect(() => likes.toggle('p-1')).not.toThrow();
    expect(likes.isLiked('p-1')).toBe(true);
  });

  it('starts empty when the browser does not allow reading', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('denied', 'SecurityError');
    });

    expect(create().count()).toBe(0);
  });

  it('follows a change made in another tab', () => {
    const likes = create();
    expect(likes.isLiked('p-9')).toBe(false);

    localStorage.setItem(LIKES_KEY, JSON.stringify(['p-9']));
    window.dispatchEvent(new StorageEvent('storage', { key: LIKES_KEY }));

    expect(likes.isLiked('p-9')).toBe(true);
  });

  it('ignores storage changes of other keys', () => {
    const likes = create();
    likes.toggle('p-1');

    localStorage.setItem('otra', '1');
    window.dispatchEvent(new StorageEvent('storage', { key: 'otra' }));

    expect(likes.isLiked('p-1')).toBe(true);
  });
});
