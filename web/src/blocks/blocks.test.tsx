import { screen } from '@testing-library/react';
import { describe, expect, test } from 'vitest';

import { renderWithProviders } from '../test/harness';

import {
  AccordionBlock,
  ButtonGroupBlock,
  CardGridBlock,
  DividerBlock,
  EmbedBlock,
  GalleryBlock,
  HeroBlock,
  IconGridBlock,
  ImageBlock,
  LogoGridBlock,
  SpacerBlock,
  TableBlock,
  TabsBlock,
  TestimonialBlock,
  TimelineBlock,
  VideoBlock,
} from './blocks';
import { coreBlocks } from './registry';

/**
 * The sixteen blocks of G3, drawn. What is asserted here is only what a schema cannot say and a
 * screenshot would not catch: that a picture points at the right address, that an address nobody
 * allowed is not framed, that a card without a link is not a link, and that an alternative text
 * left empty means "decoration" rather than "read the file name out".
 *
 * How they *look* is not here and cannot be: jsdom does no layout. The one measurement that matters
 * — three columns really being three — is in `web/e2e/`, in a browser (implementation plan §A.9).
 */

const en = (value: string) => ({ en: value, it: value });

function draw(ui: React.ReactNode) {
  return renderWithProviders(ui);
}

test('every block draws itself from the example the registry declares', () => {
  // The cheapest net there is: a block whose component and whose schema disagree throws here
  // rather than in front of a coordinator opening the gallery.
  for (const block of coreBlocks) {
    const Component = block.component;
    expect(() => draw(<Component props={block.example} />).unmount()).not.toThrow();
  }
});

describe('content', () => {
  test('a hero draws a button only for a side that has somewhere to go', () => {
    draw(
      <HeroBlock
        props={{
          title: en('What we do'),
          align: 'left',
          tone: 'muted',
          primary: { label: en('Start here'), href: '/start' },
          secondary: { label: en('Nowhere'), href: '' },
        }}
      />,
    );

    expect(screen.getByRole('heading', { name: 'What we do' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Start here' })).toHaveAttribute('href', '/start');
    expect(screen.queryByRole('link', { name: 'Nowhere' })).not.toBeInTheDocument();
  });

  test('a picture is read at the address of its file, and an empty alt means decoration', () => {
    const { unmount } = draw(<ImageBlock props={{ mediaId: 7, width: 'full', rounded: true }} />);

    // `alt=""` is what makes a screen reader skip it. The alternative — a file name — is worse than
    // nothing: it is read out, and it says nothing about the picture (design M1 §1.2).
    const decoration = document.querySelector('img');
    expect(decoration).toHaveAttribute('src', '/media/7/file');
    expect(decoration).toHaveAttribute('alt', '');
    unmount();

    draw(<ImageBlock props={{ mediaId: 7, alt: en('A runway at dawn'), width: 'full' }} />);
    expect(screen.getByAltText('A runway at dawn')).toBeInTheDocument();
  });

  test('a video is framed only from a host the allow list knows', () => {
    const { unmount } = draw(
      <VideoBlock props={{ url: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ', aspect: '16x9' }} />,
    );

    expect(document.querySelector('iframe')).toHaveAttribute(
      'src',
      'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ',
    );
    unmount();

    draw(<VideoBlock props={{ url: 'https://example.org/video.mp4', aspect: '16x9' }} />);
    expect(document.querySelector('iframe')).toBeNull();
    expect(screen.getByText(/not on a site/i)).toBeInTheDocument();
  });

  test('an embedded page carries the name it is announced by', () => {
    const { unmount } = draw(
      <EmbedBlock props={{ url: 'https://vimeo.com/76979871', title: en('The briefing'), height: 300 }} />,
    );

    expect(screen.getByTitle('The briefing')).toHaveAttribute(
      'src',
      'https://player.vimeo.com/video/76979871',
    );
    unmount();

    draw(<EmbedBlock props={{ url: 'https://example.org/page', title: en('Nowhere'), height: 300 }} />);
    expect(document.querySelector('iframe')).toBeNull();
  });

  test('a step with no icon is numbered, and a date is shown when there is one', () => {
    draw(
      <TimelineBlock
        props={{
          variant: 'timeline',
          items: [
            { title: en('First'), date: '2026-03-01T00:00:00Z' },
            { title: en('Second'), icon: 'plane' },
          ],
        }}
      />,
    );

    expect(screen.getByRole('heading', { name: 'First' })).toBeInTheDocument();
    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText(/March/)).toBeInTheDocument();
    // The second entry has an icon, so it is not numbered.
    expect(screen.queryByText('2')).not.toBeInTheDocument();
  });

  test('a table draws its caption, its headings and its cells', () => {
    draw(
      <TableBlock
        props={{
          caption: en('Opening hours'),
          columns: [
            { label: en('Day'), align: 'left' },
            { label: en('From'), align: 'right' },
          ],
          rows: [{ cells: [{ text: en('Monday') }, { text: en('18:00z') }] }],
        }}
      />,
    );

    expect(screen.getByText('Opening hours')).toBeInTheDocument();
    expect(screen.getByRole('columnheader', { name: 'Day' })).toBeInTheDocument();
    expect(screen.getByRole('cell', { name: '18:00z' })).toBeInTheDocument();
  });
});

describe('layout and containers', () => {
  test('a card is a link only when it has an address', () => {
    draw(
      <CardGridBlock
        props={{
          columns: 3,
          cards: [{ title: en('Linked'), href: '/start' }, { title: en('Not linked') }],
        }}
      />,
    );

    expect(screen.getByRole('link', { name: /Linked/ })).toHaveAttribute('href', '/start');
    expect(screen.queryByRole('link', { name: /Not linked/ })).not.toBeInTheDocument();
  });

  test('an icon nobody put on the allow list draws nothing, and takes the entry with it', () => {
    // The name is a value inside an opaque document: an old page can name an icon that no longer
    // exists, and a page is not allowed to break over decoration.
    draw(
      <IconGridBlock
        props={{ columns: 3, items: [{ icon: 'notAnIconAnybodyAllowed', title: en('Still here') }] }}
      />,
    );

    expect(screen.getByRole('heading', { name: 'Still here' })).toBeInTheDocument();
  });

  test('a gallery links to the file only when it is asked to', () => {
    const { unmount } = draw(
      <GalleryBlock props={{ images: [{ mediaId: 4 }], columns: 3, lightbox: true }} />,
    );
    expect(screen.getByRole('link')).toHaveAttribute('href', '/media/4/file');
    unmount();

    draw(<GalleryBlock props={{ images: [{ mediaId: 4 }], columns: 3, lightbox: false }} />);
    expect(screen.queryByRole('link')).not.toBeInTheDocument();
  });

  test('the name of a logo is what somebody who cannot see it is told', () => {
    draw(<LogoGridBlock props={{ columns: 4, items: [{ mediaId: 9, name: 'A partner', href: '/p' }] }} />);

    expect(screen.getByAltText('A partner')).toHaveAttribute('src', '/media/9/file');
    expect(screen.getByRole('link')).toHaveAttribute('href', '/p');
  });

  test('a tab holds text and never blocks', () => {
    draw(
      <TabsBlock
        props={{
          tabs: [
            { label: en('Pilots'), body: en('Markdown **here**.') },
            { label: en('Controllers'), body: en('And here.') },
          ],
        }}
      />,
    );

    expect(screen.getByRole('tab', { name: 'Pilots' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Controllers' })).toBeInTheDocument();
    // Markdown, rendered as a tree and not as HTML: the asterisks are formatting, not text.
    expect(screen.getByText('here')).toBeInTheDocument();
  });

  test('an accordion asks its questions, and only opens what somebody opens', () => {
    draw(
      <AccordionBlock
        props={{
          allowMultiple: false,
          items: [{ question: en('How do I join?'), answer: en('From the network itself.') }],
        }}
      />,
    );

    expect(screen.getByRole('button', { name: 'How do I join?' })).toBeInTheDocument();
    expect(screen.queryByText('From the network itself.')).not.toBeInTheDocument();
  });
});

describe('interactive and structure', () => {
  test('a quotation carries who said it and what they do', () => {
    draw(
      <TestimonialBlock props={{ quote: en('The best evening.'), author: 'A member', role: en('Pilot') }} />,
    );

    expect(screen.getByText('The best evening.')).toBeInTheDocument();
    expect(screen.getByText('A member')).toBeInTheDocument();
    expect(screen.getByText('Pilot')).toBeInTheDocument();
  });

  test('a group of buttons is a group of links, whatever weight they are drawn with', () => {
    draw(
      <ButtonGroupBlock
        props={{
          align: 'center',
          buttons: [
            { label: en('Start'), href: '/start', variant: 'primary' },
            { label: en('Outside'), href: 'https://example.org', variant: 'ghost' },
          ],
        }}
      />,
    );

    expect(screen.getByRole('link', { name: 'Start' })).toHaveAttribute('href', '/start');

    // An address the hub does not own never carries our referrer and never gets a handle on the
    // window it came from.
    const outside = screen.getByRole('link', { name: 'Outside' });
    expect(outside).toHaveAttribute('target', '_blank');
    expect(outside).toHaveAttribute('rel', 'noreferrer noopener');
  });

  test('space and a divider say nothing to a reader who is being read to', () => {
    const { container, unmount } = draw(<SpacerBlock props={{ size: 'lg' }} />);
    expect(container.textContent).toBe('');
    expect(container.firstElementChild).toHaveAttribute('aria-hidden');
    unmount();

    const divider = draw(<DividerBlock props={{ variant: 'dots', spacing: 'md' }} />);
    expect(divider.container.textContent).toBe('');
  });
});
