import { formatCoordinates, parseCoordinates } from './coordinates';

describe('parseCoordinates', () => {
  it('reads the format copied from Google Maps', () => {
    expect(parseCoordinates('19.4326077, -99.1332079')).toEqual({ latitude: 19.4326077, longitude: -99.1332079 });
  });

  it.each(['19.4 -99.1', '19.4,-99.1', '19.4 ; -99.1', '  19.4 ,  -99.1  '])('accepts the separator in "%s"', (text) => {
    expect(parseCoordinates(text)).toEqual({ latitude: 19.4, longitude: -99.1 });
  });

  it('treats blank text as no location', () => {
    expect(parseCoordinates('')).toBeNull();
    expect(parseCoordinates('   ')).toBeNull();
  });

  it.each(['abc', '19.4', '19.4, -99.1, 5', '19,4, -99,1', '19.4, ', 'a, b', '91, 0', '0, 181', '-91, 0', '0, -180.5'])(
    'rejects "%s"',
    (text) => {
      expect(parseCoordinates(text)).toBe('invalid');
    },
  );

  it('accepts the limits', () => {
    expect(parseCoordinates('90, 180')).toEqual({ latitude: 90, longitude: 180 });
    expect(parseCoordinates('-90, -180')).toEqual({ latitude: -90, longitude: -180 });
  });
});

describe('formatCoordinates', () => {
  it('writes both values, or nothing when there is no location', () => {
    expect(formatCoordinates(19.4, -99.1)).toBe('19.4, -99.1');
    expect(formatCoordinates(null, null)).toBe('');
    expect(formatCoordinates(19.4, null)).toBe('');
  });
});