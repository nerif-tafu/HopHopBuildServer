const StatusItem = ({ label, value, colorClass }) => (
    <div className="bg-neutral-800 p-4 rounded-lg flex flex-col sm:flex-row gap-2 sm:justify-between sm:items-center">
        <span className="text-neutral-400 text-sm">{label}:</span>
        <span className={`${colorClass || 'text-white'} font-mono`}>{value}</span>
    </div>
); 