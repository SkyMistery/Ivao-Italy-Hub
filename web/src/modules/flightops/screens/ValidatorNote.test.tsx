import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { I18nextProvider } from 'react-i18next';
import { describe, expect, test, vi } from 'vitest';

import { createTestI18n } from '../../../test/harness';
import type { PirepDto } from '../api';
import englishFlightOps from '../locales/en/flightops.json';

import { ValidatorNote } from './ValidatorNote';

/**
 * Issue #387: the pilot reads on the site what the validator told them in the mail — the note to the pilot and the rules the
 * decision says were broken — on a decided report only.
 */

// The bootstrap never answers: the note does not wait for it.
const api = vi.hoisted(() => ({ get: vi.fn(() => new Promise(() => undefined)) }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const words = englishFlightOps.public;

function draw(report: Pick<PirepDto, 'status' | 'noteToPilot' | 'violatedRules'>) {
  const i18n = createTestI18n();
  i18n.addResourceBundle('en', 'flightops', englishFlightOps);

  return render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient()}>
        <ValidatorNote report={report} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

describe('the validator note on the pilot’s report', () => {
  test('«to modify» says what to correct', () => {
    draw({ status: 'ToModify', noteToPilot: 'The track does not follow the SID RAVA5A.', violatedRules: [] });

    expect(screen.getByText(words.validatorNote)).toBeInTheDocument();
    expect(screen.getByText('The track does not follow the SID RAVA5A.')).toBeInTheDocument();
    expect(screen.queryByText(words.violatedRules)).toBeNull();
  });

  test('a rejection names the rules broken, with or without a note', () => {
    draw({
      status: 'Rejected',
      noteToPilot: null,
      violatedRules: [{ code: 'R-07', title: { en: 'Hold short of the runway' } }],
    });

    expect(screen.getByText(words.validatorNote)).toBeInTheDocument();
    expect(screen.getByText(words.violatedRules)).toBeInTheDocument();
    expect(screen.getByText('R-07')).toBeInTheDocument();
    expect(screen.getByText(/Hold short of the runway/)).toBeInTheDocument();
  });

  test('a report back in the queue, or a decision that said nothing, shows nothing', () => {
    const queued = draw({
      status: 'Queued',
      noteToPilot: 'The old note, already corrected.',
      violatedRules: [],
    });
    expect(queued.container).toBeEmptyDOMElement();
    queued.unmount();

    const silent = draw({ status: 'Accepted', noteToPilot: '  ', violatedRules: [] });
    expect(silent.container).toBeEmptyDOMElement();
  });
});
