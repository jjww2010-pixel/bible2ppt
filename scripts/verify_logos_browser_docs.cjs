// Read public help pages with a normal browser. Do not log in or solve challenges.
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

async function main() {
  const output = process.argv[2];
  const records = [];
  fs.writeFileSync(path.join(output, 'browser-report.json'), JSON.stringify({ status: 'not completed' }));
  const browser = await chromium.launch();
  const page = await browser.newPage();
  const seen = new Set();
  async function read(url) {
    if (seen.has(url)) return;
    seen.add(url);
    const record = { url };
    try {
      const response = await page.goto(url, { waitUntil: 'domcontentloaded', timeout: 30000 });
      await page.waitForTimeout(3000);
      record.status = response?.status();
      record.final_url = page.url();
      record.title = await page.title();
      const text = await page.locator('body').innerText({ timeout: 5000 });
      const blocked = /just a moment|access denied|verify you are human|checking your browser|security verification|unusual traffic/i.test(record.title + '\n' + text.slice(0, 1200));
      record.access_blocked = blocked || record.status === 403;
      record.documentation_links = [];
      record.excerpts = [];
      if (!record.access_blocked) {
        const links = await page.locator('a[href]').evaluateAll(elements => elements.map(el => ({ href: el.href, label: el.innerText })));
        record.documentation_links = links.filter(link => {
          try {
            const parsed = new URL(link.href);
            return ['support.logos.com', 'community.logos.com'].includes(parsed.hostname) && /print.?export|copy.?bible.?verses/i.test(link.href + ' ' + link.label);
          } catch { return false; }
        }).slice(0, 15);
        if (new URL(record.final_url).hostname.endsWith('bing.com')) {
          const results = await page.locator('li.b_algo').evaluateAll(elements => elements.map(el => ({ text: el.innerText, url: el.querySelector('h2 a')?.href })));
          record.search_snippets = [];
          for (const result of results.slice(0, 8)) {
            if (!/logos|bible/i.test(result.text)) continue;
            record.search_snippets.push({ url: result.url, text: result.text.split(/\s+/).slice(0, 65).join(' ') });
          }
        }
        if (/\/articles\/|\/kb\/articles\//.test(new URL(record.final_url).pathname)) {
          let article = page.locator('article');
          if (!(await article.count())) article = page.locator('main');
          const body = (await article.count()) ? await article.first().innerText() : text;
          const lines = body.split(/\n+/).map(line => line.trim()).filter(Boolean);
          let remaining = 80;
          for (const line of lines) {
            if (!/limit|maximum|100.{0,15}page|entire|whole|text document|plain text|save as|export|copy bible verses/i.test(line)) continue;
            const words = line.split(/\s+/);
            const take = Math.min(words.length, remaining, 30);
            if (take) record.excerpts.push(words.slice(0, take).join(' ') + (take < words.length ? ' …' : ''));
            remaining -= take;
            if (!remaining) break;
          }
        }
      }
    } catch (error) { record.error = String(error); }
    records.push(record);
    return record;
  }
  try {
    // Official hosts were already checked and returned a browser challenge.
    // Read public search summaries without retrying or bypassing that challenge.
    await read('https://www.bing.com/search?q=Logos+Print+Export+100+pages+limit');
    await read('https://www.bing.com/search?q=Logos+Copy+Bible+Verses+entire+Bible+limit');
  } finally {
    await browser.close();
    const report = { checked_utc: new Date().toISOString(), mac_application_execution: 'NOT TESTED', records };
    fs.writeFileSync(path.join(output, 'browser-report.json'), JSON.stringify(report, null, 2));
    const lines = ['\n## Browser verification', '', 'These requests use a normal browser without logging in, solving CAPTCHAs, or changing browser security settings.', ''];
    for (const record of records) {
      lines.push(record.url, '', `Status: ${record.status ?? 'unknown'}; title: ${record.title ?? 'unknown'}; blocked: ${record.access_blocked ?? 'unknown'}`, '');
      if (record.error) lines.push(record.error, '');
      for (const excerpt of record.excerpts || []) lines.push('> ' + excerpt);
      for (const snippet of record.search_snippets || []) lines.push('Search result: ' + snippet.url, '', '> ' + snippet.text, '');
      lines.push('');
    }
    fs.appendFileSync(path.join(output, 'README.md'), lines.join('\n'));
    console.log(JSON.stringify(report, null, 2));
  }
}

main().catch(error => { console.error(String(error)); process.exitCode = 1; });
