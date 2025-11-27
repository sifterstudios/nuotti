// Nuotti Feedback System - Haptics and Animations
window.nuottiFeedback = (function() {
    let dotNetRef = null;
    let reducedMotionQuery = null;

    // Initialize the feedback system
    function initialize(dotNetReference) {
        dotNetRef = dotNetReference;
        
        // Set up reduced motion listener
        if (window.matchMedia) {
            reducedMotionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');
            reducedMotionQuery.addEventListener('change', handleReducedMotionChange);
        }
    }

    function handleReducedMotionChange(e) {
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnReducedMotionChanged', e.matches);
        }
    }

    // Haptic feedback
    function haptic(type) {
        if (!navigator.vibrate) return;
        
        const patterns = {
            light: [10],
            medium: [20],
            heavy: [30]
        };
        
        const pattern = patterns[type] || patterns.light;
        navigator.vibrate(pattern);
    }

    // Confetti animations
    function confetti(type) {
        if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return; // Respect reduced motion preference
        }

        const colors = {
            success: ['#46B283', '#06BEE1', '#FF6B35'],
            celebration: ['#FFD700', '#FF6B35', '#1B9AAA', '#46B283'],
            fireworks: ['#FF6B35', '#004E89', '#1B9AAA', '#F77F00', '#EF476F']
        };

        const confettiColors = colors[type] || colors.success;
        
        // Create confetti elements
        const confettiCount = type === 'fireworks' ? 50 : 30;
        const container = document.body;
        
        for (let i = 0; i < confettiCount; i++) {
            createConfettiPiece(container, confettiColors);
        }
    }

    function createConfettiPiece(container, colors) {
        const confetti = document.createElement('div');
        confetti.style.cssText = `
            position: fixed;
            width: 8px;
            height: 8px;
            background: ${colors[Math.floor(Math.random() * colors.length)]};
            border-radius: 50%;
            pointer-events: none;
            z-index: 9999;
            left: ${Math.random() * 100}vw;
            top: -10px;
            animation: confettiFall ${2 + Math.random() * 3}s ease-out forwards;
            transform: rotate(${Math.random() * 360}deg);
        `;
        
        container.appendChild(confetti);
        
        // Remove after animation
        setTimeout(() => {
            if (confetti.parentNode) {
                confetti.parentNode.removeChild(confetti);
            }
        }, 5000);
    }

    // Pulse animation
    function pulse(elementId) {
        if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return;
        }

        const element = document.getElementById(elementId);
        if (!element) return;
        
        element.style.animation = 'none';
        element.offsetHeight; // Trigger reflow
        element.style.animation = 'nuottiPulse 0.6s ease-out';
        
        setTimeout(() => {
            element.style.animation = '';
        }, 600);
    }

    // Shake animation
    function shake(elementId) {
        if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return;
        }

        const element = document.getElementById(elementId);
        if (!element) return;
        
        element.style.animation = 'none';
        element.offsetHeight; // Trigger reflow
        element.style.animation = 'nuottiShake 0.5s ease-in-out';
        
        setTimeout(() => {
            element.style.animation = '';
        }, 500);
    }

    // Cleanup
    function dispose() {
        if (reducedMotionQuery) {
            reducedMotionQuery.removeEventListener('change', handleReducedMotionChange);
        }
        dotNetRef = null;
    }

    // Public API
    return {
        initialize,
        haptic,
        confetti,
        pulse,
        shake,
        dispose
    };
})();

// Add CSS animations
const style = document.createElement('style');
style.textContent = `
    @keyframes confettiFall {
        0% {
            transform: translateY(-10px) rotate(0deg);
            opacity: 1;
        }
        100% {
            transform: translateY(100vh) rotate(720deg);
            opacity: 0;
        }
    }

    @keyframes nuottiPulse {
        0% {
            transform: scale(1);
        }
        50% {
            transform: scale(1.05);
        }
        100% {
            transform: scale(1);
        }
    }

    @keyframes nuottiShake {
        0%, 100% {
            transform: translateX(0);
        }
        10%, 30%, 50%, 70%, 90% {
            transform: translateX(-5px);
        }
        20%, 40%, 60%, 80% {
            transform: translateX(5px);
        }
    }

    /* Respect reduced motion preference */
    @media (prefers-reduced-motion: reduce) {
        @keyframes confettiFall {
            0% {
                opacity: 1;
            }
            100% {
                opacity: 0;
            }
        }
        
        @keyframes nuottiPulse {
            0%, 100% {
                transform: scale(1);
            }
        }
        
        @keyframes nuottiShake {
            0%, 100% {
                transform: translateX(0);
            }
        }
    }
`;
document.head.appendChild(style);
