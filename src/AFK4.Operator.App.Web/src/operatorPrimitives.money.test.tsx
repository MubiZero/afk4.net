import { describe, expect, it, afterEach } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { Money } from './operatorPrimitives';

afterEach(cleanup);

describe('Money', () => {
  it('renders unsigned amount with the localized currency sign', () => {
    render(<Money minorUnits={45000} currencyCode="TJS" />);
    expect(screen.getByText('450 с.')).toHaveClass('ui-money');
  });

  it('prefixes + and marks positive tone when signed', () => {
    render(<Money minorUnits={50000} currencyCode="TJS" signed />);
    expect(screen.getByText('+500 с.')).toHaveClass('ui-money--pos');
  });

  it('prefixes − and marks negative tone when signed', () => {
    render(<Money minorUnits={-12000} currencyCode="TJS" signed />);
    expect(screen.getByText('−120 с.')).toHaveClass('ui-money--neg');
  });
});
