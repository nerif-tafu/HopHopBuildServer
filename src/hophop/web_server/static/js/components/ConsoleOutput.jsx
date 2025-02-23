const ConsoleOutput = ({ output }) => {
    const outputRef = React.useRef();

    React.useEffect(() => {
        if (outputRef.current) {
            outputRef.current.scrollTop = outputRef.current.scrollHeight;
        }
    }, [output]);

    return (
        <div 
            ref={outputRef}
            className="bg-surface-light p-4 rounded-lg whitespace-pre-wrap flex-1 overflow-y-auto text-sm leading-relaxed min-h-[100px] text-neutral-900"
        >
            {output}
        </div>
    );
}; 