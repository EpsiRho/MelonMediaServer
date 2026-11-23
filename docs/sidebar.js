// Every page loads the sidebar from sidebar.html, then builds its own "On this page" list and the
// previous / next links at the bottom from the sidebar's order.
tidyCode();

fetch('/sidebar.html')
    .then(response => response.text())
    .then(data => {
        document.getElementById('sidebar-placeholder').innerHTML = data;
        generateTOC();
        markCurrentPage();
        addPager();
    })
    .catch(error => console.error('Error loading sidebar:', error));

// Code blocks written indented inside the HTML keep that indent. Strip the blank first / last lines
// and the indent every line shares, so the code starts at the left edge.
function tidyCode() {
    document.querySelectorAll('pre > code').forEach(code => {
        if (code.children.length) return; // already highlighted by hand
        const lines = code.textContent.replace(/\t/g, '    ').split('\n');
        while (lines.length && !lines[0].trim()) lines.shift();
        while (lines.length && !lines[lines.length - 1].trim()) lines.pop();
        const indent = Math.min(...lines.filter(l => l.trim()).map(l => l.match(/^ */)[0].length));
        code.textContent = lines.map(l => l.slice(indent)).join('\n');
    });
}

// Builds the "On this page" list from the page's h1-h3 headings.
function generateTOC() {
    const toc = document.getElementById('toc');
    const headers = [...document.querySelectorAll('.content h1, .content h2, .content h3')];
    const used = new Set();
    headers.forEach(h => {
        // Headings without an id get one from their text, so they can be linked to.
        if (!h.id) {
            let id = h.textContent.toLowerCase().trim().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
            while (used.has(id) || document.getElementById(id)) id += '-';
            h.id = id;
        }
        used.add(h.id);
    });
    if (!headers.length) {
        toc.closest('.toc-panel').hidden = true;
        return;
    }

    let tocStack = [{ level: 0, ul: toc }];
    headers.forEach(header => {
        const level = parseInt(header.tagName.substring(1));
        const link = document.createElement('a');
        link.href = '#' + header.id;
        link.textContent = header.textContent;
        const listItem = document.createElement('li');
        listItem.appendChild(link);

        let lastItem = tocStack[tocStack.length - 1];
        if (level > lastItem.level && lastItem.ul.lastElementChild) {
            // Deeper heading: nest it under the last item.
            const newUl = document.createElement('ul');
            lastItem.ul.lastElementChild.appendChild(newUl);
            newUl.appendChild(listItem);
            tocStack.push({ level: level, ul: newUl });
            return;
        }
        // Same or shallower heading: walk back up to its parent list.
        while (level < lastItem.level && tocStack.length > 1) {
            tocStack.pop();
            lastItem = tocStack[tocStack.length - 1];
        }
        lastItem.ul.appendChild(listItem);
        lastItem.level = level;
    });
}

// The links in the docs nav, in the order they appear.
function navLinks() {
    return [...document.querySelectorAll('.docs-nav a[href]')];
}

const samePage = a => new URL(a.href, location.href).pathname.replace(/\/$/, '/index.html') === location.pathname.replace(/\/$/, '/index.html');

// Highlights this page in the nav and opens the section it's in.
function markCurrentPage() {
    const current = navLinks().find(samePage);
    if (!current) return;
    current.classList.add('current');
    const section = current.closest('details');
    if (section) section.open = true;
}

// Adds previous / next links to the bottom of the page.
function addPager() {
    const links = navLinks();
    const index = links.findIndex(samePage);
    if (index < 0) return;
    const pager = document.createElement('nav');
    pager.className = 'pager';
    const card = (a, label, side) => {
        const el = document.createElement('a');
        el.href = a.getAttribute('href');
        el.className = side;
        const section = a.closest('details')?.querySelector('summary')?.textContent;
        el.innerHTML = `<span class="pager-label">${label}${section ? ' · ' + section : ''}</span><span class="pager-title"></span>`;
        el.querySelector('.pager-title').textContent = a.textContent;
        return el;
    };
    pager.appendChild(index > 0 ? card(links[index - 1], '← Previous', 'prev') : document.createElement('span'));
    if (index < links.length - 1) pager.appendChild(card(links[index + 1], 'Next →', 'next'));
    document.querySelector('.content').appendChild(pager);
}
