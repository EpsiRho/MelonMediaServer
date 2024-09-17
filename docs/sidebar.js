// Load the sidebar dynamically
fetch('/sidebar.html')
    .then(response => response.text())
    .then(data => {
        document.getElementById('sidebar-placeholder').innerHTML = data;
        // After loading the sidebar, generate the Table of Contents
        generateTOC();
    })
    .catch(error => console.error('Error loading sidebar:', error));
    
// Function to generate the Table of Contents with hierarchy
function generateTOC() {
    const toc = document.getElementById('toc');
    const headers = document.querySelectorAll('.content h1, .content h2, .content h3');

    let tocStack = [{ level: 0, ul: toc }];

    headers.forEach(header => {
        const level = parseInt(header.tagName.substring(1));

        const link = document.createElement('a');
        link.href = '#' + header.id;
        link.textContent = header.textContent;

        const listItem = document.createElement('li');
        listItem.appendChild(link);

        let lastItem = tocStack[tocStack.length - 1];

        if (level > lastItem.level) {
            // Create a new nested list under the last listItem
            const newUl = document.createElement('ul');
            // Check if lastElementChild exists
            const lastLi = lastItem.ul.lastElementChild;
            if (lastLi) {
                lastLi.appendChild(newUl);
                newUl.appendChild(listItem);
                tocStack.push({ level: level, ul: newUl });
            } else {
                // If there's no last list item, append directly to the current ul
                lastItem.ul.appendChild(listItem);
            }
        } else {
            // Go back to the correct parent level
            while (level < lastItem.level && tocStack.length > 1) {
                tocStack.pop();
                lastItem = tocStack[tocStack.length - 1];
            }
            lastItem.ul.appendChild(listItem);
            // Update the current level in the stack
            if (level !== lastItem.level) {
                tocStack.pop();
                tocStack.push({ level: level, ul: lastItem.ul });
            }
        }
    });
}
