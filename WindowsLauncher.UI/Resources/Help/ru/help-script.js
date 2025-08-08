// WindowsLauncher Help System JavaScript - Russian Version

class HelpSystem {
    constructor() {
        this.currentSection = 'welcome';
        this.searchIndex = [];
        this.init();
    }

    init() {
        this.setupNavigation();
        this.setupSearch();
        this.buildSearchIndex();
        this.setupKeyboardNavigation();
        this.showSection('welcome');
        
        // Initialize with welcome section
        this.updateActiveNavLink('welcome');
    }

    setupNavigation() {
        // Navigation links
        const navLinks = document.querySelectorAll('.nav-link');
        navLinks.forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                const targetId = link.getAttribute('href').substring(1);
                this.showSection(targetId);
                this.updateActiveNavLink(targetId);
            });
        });

        // Quick start cards
        window.navigateToSection = (sectionId) => {
            this.showSection(sectionId);
            this.updateActiveNavLink(sectionId);
        };
    }

    setupSearch() {
        const searchInput = document.getElementById('searchInput');
        const clearSearch = document.getElementById('clearSearch');

        searchInput.addEventListener('input', (e) => {
            const query = e.target.value.trim();
            if (query.length >= 2) {
                this.performSearch(query);
            } else {
                this.clearSearch();
            }
        });

        searchInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                const query = searchInput.value.trim();
                if (query.length >= 2) {
                    this.performSearch(query);
                }
            }
        });

        clearSearch.addEventListener('click', () => {
            searchInput.value = '';
            this.clearSearch();
            searchInput.focus();
        });
    }

    setupKeyboardNavigation() {
        document.addEventListener('keydown', (e) => {
            // Escape key clears search
            if (e.key === 'Escape') {
                const searchInput = document.getElementById('searchInput');
                searchInput.value = '';
                this.clearSearch();
            }

            // Ctrl/Cmd + K focuses search
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                document.getElementById('searchInput').focus();
            }

            // Arrow keys for section navigation
            if (e.altKey) {
                if (e.key === 'ArrowRight') {
                    e.preventDefault();
                    this.nextSection();
                } else if (e.key === 'ArrowLeft') {
                    e.preventDefault();
                    this.prevSection();
                }
            }
        });
    }

    buildSearchIndex() {
        const sections = document.querySelectorAll('.content-section');
        sections.forEach(section => {
            const sectionId = section.id;
            const title = section.querySelector('h2')?.textContent || '';
            const content = this.extractTextContent(section);
            
            this.searchIndex.push({
                id: sectionId,
                title: title,
                content: content.toLowerCase(),
                element: section
            });
        });
    }

    extractTextContent(element) {
        // Get all text content, excluding script and style elements
        const clone = element.cloneNode(true);
        const scripts = clone.querySelectorAll('script, style');
        scripts.forEach(script => script.remove());
        return clone.textContent || clone.innerText || '';
    }

    performSearch(query) {
        const results = this.searchInContent(query);
        this.displaySearchResults(results, query);
    }

    searchInContent(query) {
        const searchTerms = query.toLowerCase().split(' ').filter(term => term.length > 1);
        const results = [];

        this.searchIndex.forEach(item => {
            let score = 0;
            let matches = [];

            searchTerms.forEach(term => {
                // Title matches get higher score
                if (item.title.toLowerCase().includes(term)) {
                    score += 10;
                    matches.push(`title:${term}`);
                }
                
                // Content matches
                const contentMatches = this.countMatches(item.content, term);
                if (contentMatches > 0) {
                    score += contentMatches * 2;
                    matches.push(`content:${term}:${contentMatches}`);
                }
            });

            if (score > 0) {
                results.push({
                    ...item,
                    score: score,
                    matches: matches,
                    relevance: this.calculateRelevance(searchTerms, item)
                });
            }
        });

        // Sort by score and relevance
        return results.sort((a, b) => {
            if (b.score !== a.score) return b.score - a.score;
            return b.relevance - a.relevance;
        });
    }

    countMatches(text, term) {
        const regex = new RegExp(term, 'gi');
        const matches = text.match(regex);
        return matches ? matches.length : 0;
    }

    calculateRelevance(searchTerms, item) {
        const allTermsInTitle = searchTerms.every(term => 
            item.title.toLowerCase().includes(term)
        );
        const allTermsInContent = searchTerms.every(term => 
            item.content.includes(term)
        );

        let relevance = 0;
        if (allTermsInTitle) relevance += 50;
        if (allTermsInContent) relevance += 20;
        
        return relevance;
    }

    displaySearchResults(results, query) {
        if (results.length === 0) {
            this.showNoResults(query);
            return;
        }

        // Show the most relevant section
        const bestMatch = results[0];
        this.showSection(bestMatch.id);
        this.updateActiveNavLink(bestMatch.id);

        // Highlight search terms in the content
        this.highlightSearchTerms(query);

        // Update status
        this.updateSearchStatus(`Найдено результатов: ${results.length}`);
    }

    showNoResults(query) {
        this.updateSearchStatus(`Ничего не найдено по запросу: "${query}"`);
    }

    highlightSearchTerms(query) {
        const activeSection = document.querySelector('.content-section.active');
        if (!activeSection) return;

        const searchTerms = query.toLowerCase().split(' ').filter(term => term.length > 1);
        
        // Remove previous highlights
        this.clearHighlights(activeSection);

        // Apply new highlights
        searchTerms.forEach(term => {
            this.highlightTerm(activeSection, term);
        });
    }

    clearHighlights(element) {
        const highlights = element.querySelectorAll('.search-highlight');
        highlights.forEach(highlight => {
            const parent = highlight.parentNode;
            parent.replaceChild(document.createTextNode(highlight.textContent), highlight);
            parent.normalize();
        });
    }

    highlightTerm(element, term) {
        const walker = document.createTreeWalker(
            element,
            NodeFilter.SHOW_TEXT,
            null,
            false
        );

        const textNodes = [];
        let node;
        while (node = walker.nextNode()) {
            if (node.nodeValue.trim()) {
                textNodes.push(node);
            }
        }

        textNodes.forEach(textNode => {
            const text = textNode.nodeValue;
            const regex = new RegExp(`(${this.escapeRegExp(term)})`, 'gi');
            
            if (regex.test(text)) {
                const highlightedText = text.replace(regex, '<span class="search-highlight">$1</span>');
                const tempDiv = document.createElement('div');
                tempDiv.innerHTML = highlightedText;
                
                const fragment = document.createDocumentFragment();
                while (tempDiv.firstChild) {
                    fragment.appendChild(tempDiv.firstChild);
                }
                
                textNode.parentNode.replaceChild(fragment, textNode);
            }
        });
    }

    escapeRegExp(string) {
        return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }

    clearSearch() {
        // Remove all highlights
        const highlights = document.querySelectorAll('.search-highlight');
        highlights.forEach(highlight => {
            const parent = highlight.parentNode;
            parent.replaceChild(document.createTextNode(highlight.textContent), highlight);
            parent.normalize();
        });

        this.updateSearchStatus('');
    }

    updateSearchStatus(message) {
        // You can implement status updates here if needed
        console.log('Search status:', message);
    }

    showSection(sectionId) {
        // Hide all sections
        const sections = document.querySelectorAll('.content-section');
        sections.forEach(section => {
            section.classList.remove('active');
        });

        // Show target section
        const targetSection = document.getElementById(sectionId);
        if (targetSection) {
            targetSection.classList.add('active');
            this.currentSection = sectionId;
            
            // Scroll to top of content
            targetSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }
    }

    updateActiveNavLink(sectionId) {
        // Remove active class from all nav links
        const navLinks = document.querySelectorAll('.nav-link');
        navLinks.forEach(link => {
            link.classList.remove('active');
        });

        // Add active class to current nav link
        const activeLink = document.querySelector(`.nav-link[href="#${sectionId}"]`);
        if (activeLink) {
            activeLink.classList.add('active');
        }
    }

    getSectionOrder() {
        return [
            'welcome',
            'getting-started', 
            'interface',
            'applications',
            'categories',
            'email',
            'app-switcher',
            'troubleshooting',
            'shortcuts'
        ];
    }

    nextSection() {
        const order = this.getSectionOrder();
        const currentIndex = order.indexOf(this.currentSection);
        const nextIndex = (currentIndex + 1) % order.length;
        
        this.showSection(order[nextIndex]);
        this.updateActiveNavLink(order[nextIndex]);
    }

    prevSection() {
        const order = this.getSectionOrder();
        const currentIndex = order.indexOf(this.currentSection);
        const prevIndex = currentIndex > 0 ? currentIndex - 1 : order.length - 1;
        
        this.showSection(order[prevIndex]);
        this.updateActiveNavLink(order[prevIndex]);
    }

    // Public API for external navigation
    navigateTo(sectionId) {
        this.showSection(sectionId);
        this.updateActiveNavLink(sectionId);
    }

    // Public API for search
    search(query) {
        const searchInput = document.getElementById('searchInput');
        searchInput.value = query;
        if (query.length >= 2) {
            this.performSearch(query);
        }
    }
}

// Initialize help system when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.helpSystem = new HelpSystem();
    
    // Smooth scrolling for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            const targetId = this.getAttribute('href').substring(1);
            const targetElement = document.getElementById(targetId);
            
            if (targetElement) {
                targetElement.scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // Add loading animation for better UX
    document.body.classList.add('loaded');
});

// Keyboard shortcuts help
document.addEventListener('keydown', (e) => {
    // Show shortcuts on F1
    if (e.key === 'F1') {
        e.preventDefault();
        window.helpSystem.navigateTo('shortcuts');
    }
    
    // Quick navigation with numbers
    if (e.altKey && e.key >= '1' && e.key <= '9') {
        e.preventDefault();
        const sectionOrder = window.helpSystem.getSectionOrder();
        const index = parseInt(e.key) - 1;
        if (index < sectionOrder.length) {
            window.helpSystem.navigateTo(sectionOrder[index]);
        }
    }
});

// Export for potential external use
if (typeof module !== 'undefined' && module.exports) {
    module.exports = HelpSystem;
}